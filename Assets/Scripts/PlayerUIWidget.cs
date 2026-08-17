using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUIWidget : MonoBehaviour
{
    public static PlayerUIWidget Instance { get; private set; }
    public bool HasPass { get; private set; } = false;

    [SerializeField] private GameObject healthGroup;
    [SerializeField] private GameObject statsIconGroup;

    [Header("Pasek i Licznik Życia")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Ikona Przepustki")]
    [SerializeField] private Image passIconImage;
    [SerializeField] private Sprite passColorSprite;
    [SerializeField] private Sprite passGrayscaleSprite;

    public void UpdatePassStatus(bool hasPass){
        HasPass = hasPass;

        if (passIconImage == null) return;
        passIconImage.sprite = hasPass ? passColorSprite : passGrayscaleSprite;
        passIconImage.color = Color.white;
    }

    public void HideHUD(){
        if (healthGroup != null) healthGroup.SetActive(false);
        if (statsIconGroup != null) statsIconGroup.SetActive(false);
    }

    private void Awake(){
        if (Instance != null && Instance != this) { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
    }

    private void OnEnable(){
        PlayerHealth.OnHealthChanged += UpdateHealthUI;
    }

    private void OnDisable(){
        PlayerHealth.OnHealthChanged -= UpdateHealthUI;
    }

    private void Start(){
        UpdatePassStatus(false);
    }

    private void UpdateHealthUI(float currentHealth, float maxHealth){
        float ratio = Mathf.Clamp01(currentHealth / maxHealth);

        if (healthBarFill != null) healthBarFill.fillAmount = ratio;
        if (healthText != null) healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
    }
}