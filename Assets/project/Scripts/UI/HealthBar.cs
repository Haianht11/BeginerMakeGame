using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image ghostFillImage;
    [SerializeField] private float ghostHoldDuration = 0.2f;
    [SerializeField] private float ghostCatchupSpeed = 1.8f;
    [SerializeField] private Text valueLabel;
    [SerializeField] private string valueFormat = "{0}/{1}";
    [SerializeField] private bool snapGhostOnInitialize = true;

    private bool hasInitialized;
    private float ghostHoldTimer;

    private void Update()
    {
        if (ghostFillImage == null || fillImage == null)
            return;

        float target = fillImage.fillAmount;
        if (ghostFillImage.fillAmount <= target)
        {
            ghostFillImage.fillAmount = target;
            ghostHoldTimer = 0f;
            return;
        }

        if (ghostHoldTimer > 0f)
        {
            ghostHoldTimer -= Time.unscaledDeltaTime;
            return;
        }

        ghostFillImage.fillAmount = Mathf.Lerp(
            ghostFillImage.fillAmount,
            target,
            Mathf.Clamp01(ghostCatchupSpeed * Time.unscaledDeltaTime));

        if (ghostFillImage.fillAmount - target <= 0.001f)
            ghostFillImage.fillAmount = target;
    }

    // Cập nhật tỷ lệ thanh máu (giá trị từ 0.0f đến 1.0f)
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        float normalized = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        float previousFill = fillImage != null
            ? fillImage.fillAmount
            : (ghostFillImage != null ? ghostFillImage.fillAmount : normalized);

        if (fillImage != null && maxHealth > 0f)
            fillImage.fillAmount = normalized;

        if (ghostFillImage != null)
        {
            if ((!hasInitialized && snapGhostOnInitialize) || normalized >= ghostFillImage.fillAmount)
            {
                ghostFillImage.fillAmount = normalized;
                ghostHoldTimer = 0f;
            }
            else if (normalized < previousFill)
            {
                ghostHoldTimer = ghostHoldDuration;
            }
        }

        hasInitialized = true;
        UpdateValueLabel(currentHealth, maxHealth);
    }

    public void SetPreviewHealth(float currentHealth, float maxHealth, float ghostHealth)
    {
        float normalized = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        float ghostNormalized = maxHealth > 0f ? Mathf.Clamp01(ghostHealth / maxHealth) : normalized;

        if (fillImage != null)
            fillImage.fillAmount = normalized;

        if (ghostFillImage != null)
            ghostFillImage.fillAmount = Mathf.Max(normalized, ghostNormalized);

        ghostHoldTimer = 0f;
        hasInitialized = true;
        UpdateValueLabel(currentHealth, maxHealth);
    }

    private void UpdateValueLabel(float currentHealth, float maxHealth)
    {
        if (valueLabel == null)
            return;

        int currentDisplay = Mathf.CeilToInt(Mathf.Max(0f, currentHealth));
        int maxDisplay = Mathf.CeilToInt(Mathf.Max(0f, maxHealth));
        string format = string.IsNullOrEmpty(valueFormat) ? "{0}/{1}" : valueFormat;
        valueLabel.text = string.Format(format, currentDisplay, maxDisplay);
    }
}
