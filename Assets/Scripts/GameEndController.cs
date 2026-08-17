using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameEndController : MonoBehaviour
{
    private static GameEndController _instance;
    public static GameEndController Instance{
        get{
            if (_instance == null){
                _instance = FindFirstObjectByType<GameEndController>();
                if (_instance == null){
                    GameObject go = new GameObject("GameEndController");
                    _instance = go.AddComponent<GameEndController>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Komunikaty i Czasy")]
    [SerializeField] private string victoryMessage = "Gratulacje! Pokonałeś wszystkie przeciwności i przeszedłeś całą misję!";
    [SerializeField] private float notificationDuration = 12.0f;
    [SerializeField] private float delayBeforeFade = 6.0f;
    [SerializeField] private float fadeDuration = 4.0f;
    [SerializeField] private bool freezeTimeOnEnd = true;

    [Header("Opcjonalna Własna Nakładka Fade (CanvasGroup)")]
    [SerializeField] private CanvasGroup customFadeCanvasGroup;

    private bool hasSequenceStarted = false;

    private void Awake(){
        if (_instance != null && _instance != this){
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable(){
        DarkWizardAI.OnDarkWizardDefeated += HandleBossDefeated;
    }

    private void OnDisable(){
        DarkWizardAI.OnDarkWizardDefeated -= HandleBossDefeated;
    }

    private void HandleBossDefeated(){
        StartVictorySequence();
    }

    public static void TriggerVictorySequence(){
        Instance.StartVictorySequence();
    }

    public void StartVictorySequence(){
        if (hasSequenceStarted) return;
        hasSequenceStarted = true;

        StartCoroutine(WinGameSequenceRoutine());
    }

    private IEnumerator WinGameSequenceRoutine(){
        // 1. Powiadomienie końcowe o zwycięstwie
        NotificationManager.Show(victoryMessage, NotificationType.Info, notificationDuration);

        // 2. Ukrycie interfejsu gracza
        if (PlayerUIWidget.Instance != null){
            PlayerUIWidget.Instance.HideHUD();
        }

        // 3. Oczekiwanie przed rozpoczęciem wyciemniania
        yield return new WaitForSeconds(delayBeforeFade);

        // 4. Przygotowanie lub pobranie CanvasGroup do wyciemnienia
        CanvasGroup fadeCanvasGroup = customFadeCanvasGroup;
        if (fadeCanvasGroup == null){
            fadeCanvasGroup = CreateVRFadeOverlay();
        }

        // 5. Płynne ściemnianie sceny
        float elapsed = 0f;
        while (elapsed < fadeDuration){
            elapsed += Time.unscaledDeltaTime;
            if (fadeCanvasGroup != null){
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            }
            yield return null;
        }

        if (fadeCanvasGroup != null){
            fadeCanvasGroup.alpha = 1f;
        }

        // 6. Zatrzymanie upływu czasu
        if (freezeTimeOnEnd){
            Time.timeScale = 0f;
        }
    }

    private CanvasGroup CreateVRFadeOverlay()
    {
        Transform mainCam = PlayerTargetProvider.GetPlayerTransform();
        if (mainCam == null && Camera.main != null)
        {
            mainCam = Camera.main.transform;
        }

        if (mainCam == null) return null;

        GameObject fadeObj = new GameObject("VR_WinFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        fadeObj.transform.SetParent(mainCam, false);
        fadeObj.transform.localPosition = new Vector3(0f, 0f, 1.1f);
        fadeObj.transform.localRotation = Quaternion.identity;

        Canvas fadeCanvas = fadeObj.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform rect = fadeObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 100f);

        Image fadeImage = fadeObj.GetComponent<Image>();
        fadeImage.color = Color.black;

        CanvasGroup canvasGroup = fadeObj.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        return canvasGroup;
    }
}
