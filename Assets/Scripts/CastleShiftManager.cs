using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Menedżer zmian wartowniczych na terenie zamku.
/// Odpowiada za cykliczne delegowanie strażników ze strefy odpoczynku do posterunków i z powrotem.
/// </summary>
public class CastleShiftManager : MonoBehaviour{
    [Header("Ustawienia")]
    [SerializeField] private GuardAI guardPrefab;
    [SerializeField] private int totalGuards = 40;
    [SerializeField] private float shiftInterval = 12f;
    [SerializeField] private float handoverTriggerDistance = 3.5f;

    [Header("Infrastruktura")]
    [SerializeField] private List<GuardPost> posts;
    [SerializeField] private List<Bed> beds;

    private List<GuardAI> guards = new List<GuardAI>();
    [SerializeField] private SplineContainer sharedMainPath;
    public SplineContainer SharedMainPath => sharedMainPath;

    private void Start(){
        AlertController.ResetAlert();
        SpawnGuards();
        StartCoroutine(ShiftLoop());
    }

    private void SpawnGuards(){
        if (guardPrefab == null){
            Debug.LogError("CastleShiftManager: brak przypisanego guardPrefab w Inspektorze!");
            return;
        }

        int spawned = 0;

        if (posts != null){
            foreach (var post in posts){
                if (post == null || post.Position == null) continue;
                if (spawned >= totalGuards) break;

                GuardAI guard = Instantiate(guardPrefab, post.Position.position, post.Position.rotation);
                guard.gameObject.name = $"Guard_Post_{post.postName}";
                guard.InitDuty(post, this);
                guards.Add(guard);

                if (AlertController.Instance != null){
                    AlertController.Instance.RegisterGuard(guard);
                }

                spawned++;
            }
        }

        if (beds != null){
            foreach (var bed in beds){
                if (bed == null || bed.sleepAnchor == null) continue;
                if (spawned >= totalGuards) break;

                GuardAI guard = Instantiate(guardPrefab, bed.sleepAnchor.position, bed.sleepAnchor.rotation);
                guard.gameObject.name = $"Guard_Sleeping_{spawned + 1}";
                guard.InitSleeping(bed, this);
                guards.Add(guard);

                if (AlertController.Instance != null){
                    AlertController.Instance.RegisterGuard(guard);
                }

                spawned++;
            }
        }
    }

    private IEnumerator ShiftLoop(){
        while (true){
            yield return new WaitForSeconds(shiftInterval);

            if (posts.Count == 0) continue;

            // 1. Filtracja tylko tych posterunków, na które nikt aktualnie nie idzie
            var availablePosts = posts.Where(p => p.currentGuard != null && !p.IsTargeted).ToList();

            if (availablePosts.Count == 0) continue;

            // 2. Wybieranie losowego dostępnego posterunku
            GuardPost postToChange = availablePosts[Random.Range(0, availablePosts.Count)];

            // 3. Losowanie dostępnego strażnika
            var sleepingGuards = guards.Where(g => g.CurrentState == GuardState.Sleeping && !g.isDead).ToList();

            if (sleepingGuards.Count > 0){
                GuardAI newGuard = sleepingGuards[Random.Range(0, sleepingGuards.Count)];

                // REZERWACJA POSTERUNKU
                postToChange.incomingGuard = newGuard;

                newGuard.WakeUpAndGoToPost(postToChange, handoverTriggerDistance);
            }
        }
    }

    public Bed GetAndReserveFreeBed(){
        Bed freeBed = beds.FirstOrDefault(b => b.IsAvailable);
        if (freeBed != null){
            freeBed.IsReserved = true;
            return freeBed;
        }

        Bed fallbackBed = beds.FirstOrDefault(b => !b.IsOccupied);
        if (fallbackBed != null){
            fallbackBed.IsReserved = true;
            return fallbackBed;
        }

        return beds.Count > 0 ? beds[Random.Range(0, beds.Count)] : null;
    }
}