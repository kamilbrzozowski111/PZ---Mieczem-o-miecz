using System.Collections;
using UnityEngine;

/// <summary>
/// Kontroler posterunku przy bramie zamkowej.
/// Odpowiada za weryfikację przepustki gracza i koordynację otwarcia bramy przez pełniącego służbę strażnika.
/// </summary>
public class CastleGatePostController : MonoBehaviour{
    [Header("Komponenty")]
    [SerializeField] private GuardPost guardPost;
    [SerializeField] private CastleGate gate;

    [Header("Ustawienia Interakcji")]
    [SerializeField] private float triggerDistance = 32.0f;
    [SerializeField] private float noPassMessageCooldown = 5.0f;

    private float lastNoPassMessageTime = -10.0f;
    private bool isProcessingGate = false;
    private bool hasBeenOpened = false;

    private void Update(){
        if (hasBeenOpened || gate == null || gate.IsOpen || isProcessingGate) return;

        Transform playerTransform = PlayerTargetProvider.GetPlayerTransform();
        if (playerTransform == null) return;

        // Jeśli w zamku trwa stan alarmu, brama nie zostanie otwarta pokojowo
        if (AlertController.Instance != null && AlertController.Instance.IsAlerted) return;
        if (GuardAI.hasNotifiedAllAlerted) return;
        if (guardPost == null) return;

        // 1. WYBÓR ODPOWIEDNIEGO STRAŻNIKA
        GuardAI activeGuard = null;
        GuardAI currentGuard = guardPost.currentGuard;
        GuardAI incomingGuard = guardPost.incomingGuard;

        // Jeśli nowy strażnik wszedł na ostatnią prostą do posterunku, to ON przejmuje zadanie
        if (incomingGuard != null && !incomingGuard.isDead && incomingGuard.IsOffSpline){
            activeGuard = incomingGuard;
        }
        else if (currentGuard != null && !currentGuard.isDead && currentGuard.CurrentState == GuardState.OnDuty){
            // Nowy jest jeszcze na Spline -> Stary strażnik obsługuje bramę
            activeGuard = currentGuard;
        }

        if (activeGuard == null) return;

        // 2. Sprawdzenie dystansu gracza do posterunku
        float distanceToPost = Vector3.Distance(playerTransform.position, guardPost.Position.position);
        if (distanceToPost > triggerDistance) return;

        // 3. Weryfikacja przepustki
        if (CheckPlayerPassStatus()){
            hasBeenOpened = true;
            NotificationManager.Show("Przepustka została uznana, trwa otwieranie bramy wjazdowej!", NotificationType.Info);
            StartCoroutine(OpenGateSequence(activeGuard));
        }
        else{
            HandleNoPassMessage();
        }
    }

    private bool CheckPlayerPassStatus(){
        PlayerUIWidget uiWidget = PlayerUIWidget.Instance != null ? PlayerUIWidget.Instance : FindFirstObjectByType<PlayerUIWidget>();
        return uiWidget != null && uiWidget.HasPass;
    }

    private void HandleNoPassMessage(){
        if (Time.time >= lastNoPassMessageTime + noPassMessageCooldown){
            lastNoPassMessageTime = Time.time;
            NotificationManager.Show("Aby wejść na teren zamku potrzebujesz ważnej przepustki..", NotificationType.Warning);
        }
    }

    private IEnumerator OpenGateSequence(GuardAI guard){
        isProcessingGate = true;

        while (guard != null && guard.CurrentState == GuardState.WalkingToPost){
            yield return null;
        }

        if (guard == null || guard.isDead){
            isProcessingGate = false;
            yield break;
        }

        // 1. Zablokowanie ruchu i oznaczenie interakcji
        guard.SetInteracting(true);
        guard.TriggerButtonPushAnimation();

        // 2. Czekanie na animację przycisku
        yield return new WaitForSeconds(7.2f);

        // 3. Otwieranie bramy
        if (gate != null){
            yield return StartCoroutine(gate.OpenGateRoutine());
        }

        if (guard != null){
            guard.SetInteracting(false);
        }

        isProcessingGate = false;
    }
}