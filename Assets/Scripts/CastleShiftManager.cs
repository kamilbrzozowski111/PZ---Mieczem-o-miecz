using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CastleShiftManager : MonoBehaviour
{
    [Header("Ustawienia")]
    [SerializeField] private GuardAI guardPrefab;
    [SerializeField] private int totalGuards = 20;
    [SerializeField] private float shiftInterval = 20f;
    [SerializeField] private float handoverTriggerDistance = 30f;

    [Header("Infrastruktura")]
    [SerializeField] private List<GuardPost> posts;
    [SerializeField] private List<Bed> beds;

    private List<GuardAI> guards = new List<GuardAI>();

    private void Start()
    {
        SpawnGuards();
        StartCoroutine(ShiftLoop());
    }

    private void SpawnGuards()
    {
        int spawned = 0;

        foreach (var post in posts)
        {
            if (spawned >= totalGuards) break;

            GuardAI guard = Instantiate(guardPrefab, post.Position.position, post.Position.rotation);
            guard.gameObject.name = $"Guard_Post_{post.postName}";
            guard.InitDuty(post, this);
            guards.Add(guard);
            spawned++;
        }

        foreach (var bed in beds)
        {
            if (spawned >= totalGuards) break;

            GuardAI guard = Instantiate(guardPrefab, bed.sleepAnchor.position, bed.sleepAnchor.rotation);
            guard.gameObject.name = $"Guard_Sleeping_{spawned + 1}";
            guard.InitSleeping(bed, this);
            guards.Add(guard);
            spawned++;
        }
    }

    private IEnumerator ShiftLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(shiftInterval);

            if (posts.Count == 0) continue;

            // 1. Filtrujemy tylko te posterunki, na które NIKT AKTUALNIE NIE IDZIE
            var availablePosts = posts.Where(p => p.currentGuard != null && !p.IsTargeted).ToList();

            if (availablePosts.Count == 0) continue;

            // 2. Wybieramy losowy dostępny posterunek
            GuardPost postToChange = availablePosts[Random.Range(0, availablePosts.Count)];

            // 3. Losujemy śpiącego strażnika
            var sleepingGuards = guards.Where(g => g.CurrentState == GuardState.Sleeping).ToList();

            if (sleepingGuards.Count > 0)
            {
                GuardAI newGuard = sleepingGuards[Random.Range(0, sleepingGuards.Count)];

                //Zapobiega wysłaniu kolejnego strażnika na ten sam posterunek!
                postToChange.incomingGuard = newGuard; 
                
                newGuard.WakeUpAndGoToPost(postToChange, handoverTriggerDistance);
            }
        }
    }

    public Bed GetAndReserveFreeBed()
    {
        Bed freeBed = beds.FirstOrDefault(b => b.IsAvailable);
        if (freeBed != null)
        {
            freeBed.IsReserved = true;
            return freeBed;
        }

        //zabezpieczenie: jeśli z jakiegoś powodu brakuje wolnych rezerwacji
        Bed fallbackBed = beds.FirstOrDefault(b => !b.IsOccupied);
        if (fallbackBed != null)
        {
            fallbackBed.IsReserved = true;
            return fallbackBed;
        }

        return beds.Count > 0 ? beds[Random.Range(0, beds.Count)] : null;
    }
}