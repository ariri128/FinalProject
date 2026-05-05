using UnityEngine;
using TMPro;

public class StudioRelaunchManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI industryKnowledgeText;
    public TextMeshProUGUI relaunchInfoText;

    [Header("Settings")]
    public float relaunchRevenueThreshold = 10000f;

    private const string IK_PREFS_KEY = "IndustryKnowledge";

    private int industryKnowledge = 0;
    private GameManager gameManager;

    private void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        industryKnowledge = PlayerPrefs.GetInt(IK_PREFS_KEY, 0);
        UpdateUI();
    }

    // Returns the permanent multiplier to apply to all revenue
    public float GetPermanentMultiplier()
    {
        return 1f + (industryKnowledge * 0.1f);
    }

    // Called by the Studio Relaunch button
    public void TriggerRelaunch()
    {
        industryKnowledge++;
        PlayerPrefs.SetInt(IK_PREFS_KEY, industryKnowledge);
        PlayerPrefs.Save();

        gameManager.ExecuteRelaunch();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (industryKnowledgeText != null)
            industryKnowledgeText.text = "Industry Knowledge: " + industryKnowledge;

        if (relaunchInfoText != null)
        {
            float nextMultiplier = 1f + ((industryKnowledge + 1) * 0.1f);
            relaunchInfoText.text = "Relaunch at $" + NumberFormatter.Format(relaunchRevenueThreshold)
                + " to earn Industry Knowledge\nNext relaunch bonus: x" + nextMultiplier.ToString("F1") + " permanent revenue";
        }
    }
}
