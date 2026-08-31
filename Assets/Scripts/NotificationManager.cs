using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Typy powiadomień
public enum NotificationType{
    Info,
    Warning,
    Danger
}

public class NotificationManager : MonoBehaviour{
    public static NotificationManager Instance { get; private set; }

    [Header("Komponenty UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI notificationText;

    [Header("Ustawienia Animacji")]
    [SerializeField] private float fadeSpeed = 0.25f;

    [Header("Paleta Kolorów Powiadomień")]
    [SerializeField] private Color infoColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f);

    // Kolejka przechowywująca treść, czas i kolor tekstu
    private readonly Queue<(string message, float duration, Color color)> queue = new Queue<(string, float, Color)>();
    private bool isDisplaying = false;

    private void Awake(){
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (canvasGroup) canvasGroup.alpha = 0f;
    }


    // 1. Domyślne wywołanie z typem powiadomienia
    public static void Show(string message, NotificationType type = NotificationType.Info, float duration = 3.0f){
        if (Instance != null){
            Color selectedColor = Instance.GetColorForType(type);
            Instance.EnqueueNotification(message, duration, selectedColor);
        }
    }
    public static void Show(string message, Color customColor, float duration = 3.0f){
        if (Instance != null){
            Instance.EnqueueNotification(message, duration, customColor);
        }
    }


    public void EnqueueNotification(string message, float duration, Color color){
        queue.Enqueue((message, duration, color));
        if (!isDisplaying){
            StartCoroutine(DisplayRoutine());
        }
    }

    private IEnumerator DisplayRoutine(){
        isDisplaying = true;

        while (queue.Count > 0){
            var item = queue.Dequeue();

            if (notificationText != null){
                notificationText.text = item.message;
                notificationText.color = item.color;
            }

            while (canvasGroup != null && canvasGroup.alpha < 1f){
                canvasGroup.alpha += Time.deltaTime / fadeSpeed;
                yield return null;
            }

            yield return new WaitForSeconds(item.duration);

            while (canvasGroup != null && canvasGroup.alpha > 0f){
                canvasGroup.alpha -= Time.deltaTime / fadeSpeed;
                yield return null;
            }
        }

        isDisplaying = false;
    }

    private Color GetColorForType(NotificationType type){
        switch (type){
            case NotificationType.Info:
                return infoColor;
            case NotificationType.Warning:
                return warningColor;
            case NotificationType.Danger:
                return dangerColor;
            default:
                return Color.white;
        }
    }
}