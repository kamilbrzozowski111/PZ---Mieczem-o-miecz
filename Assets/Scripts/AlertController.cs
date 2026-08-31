using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dedykowany kontroler zarządzający stanem alarmu na terenie zamku.
/// </summary>
public class AlertController : MonoBehaviour{
    private static AlertController _instance;
    public static AlertController Instance{
        get{
            if (_instance == null){
                _instance = FindFirstObjectByType<AlertController>();
                if (_instance == null){
                    GameObject go = new GameObject("AlertController");
                    _instance = go.AddComponent<AlertController>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Ustawienia Powiadomień")]
    [SerializeField] private string alertMessage = "Wszyscy strażnicy zostali zaalarmowani!";
    [SerializeField] private float alertNotificationDuration = 4.0f;

    public bool IsAlerted { get; private set; } = false;

    public static event Action<Transform> OnAlertTriggered;

    private readonly List<GuardAI> registeredGuards = new List<GuardAI>();

    private void Awake(){
        if (_instance != null && _instance != this){
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// Rejestruje strażnika w systemie alarmowym.
    /// </summary>
    public void RegisterGuard(GuardAI guard){
        if (guard != null && !registeredGuards.Contains(guard)){
            registeredGuards.Add(guard);
        }
    }

    /// <summary>
    /// Wyrejestrowuje strażnika z systemu alarmowego.
    /// </summary>
    public void UnregisterGuard(GuardAI guard){
        if (guard != null){
            registeredGuards.Remove(guard);
        }
    }

    /// <summary>
    /// Wyzwala alarm na całej scenie.
    /// </summary>
    public static void TriggerAlert(Transform playerTransform){
        if (Instance != null){
            Instance.ExecuteAlert(playerTransform);
        }
        else{
            OnAlertTriggered?.Invoke(playerTransform);
        }
    }

    private void ExecuteAlert(Transform playerTransform){
        if (playerTransform == null){
            playerTransform = PlayerTargetProvider.GetPlayerTransform();
        }

        bool wasAlertedBefore = IsAlerted;
        IsAlerted = true;
        GuardAI.SetLegacyAlertedFlag(true);

        if (!wasAlertedBefore){
            NotificationManager.Show(alertMessage, NotificationType.Danger, alertNotificationDuration);
        }

        // 1. Wywołanie zdarzenia dla subskrybentów
        OnAlertTriggered?.Invoke(playerTransform);

        // 2. Bezpośrednie zaalarmowanie zarejestrowanych strażników
        for (int i = registeredGuards.Count - 1; i >= 0; i--){
            GuardAI guard = registeredGuards[i];
            if (guard != null && !guard.isDead){
                guard.AlertGuard(playerTransform);
            }
            else if (guard == null){
                registeredGuards.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Resetuje stan alarmu
    /// </summary>
    public static void ResetAlert(){
        if (_instance != null){
            _instance.IsAlerted = false;
        }
        GuardAI.SetLegacyAlertedFlag(false);
    }
}
