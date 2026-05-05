using UnityEngine;

public class Upgrade
{
    public string upgradeName;
    public float cost;
    public UpgradeEffect effect;
    public int tier;
    public UpgradeState state;
    public int purchaseCount;
    public int maxPurchases = 2;

    public Upgrade(string name, float cost, UpgradeEffect effect, int tier, UpgradeState state)
    {
        this.upgradeName = name;
        this.cost = cost;
        this.effect = effect;
        this.tier = tier;
        this.state = state;
        this.purchaseCount = 0;
    }

    public bool IsFullyPurchased()
    {
        return purchaseCount >= maxPurchases;
    }
}
