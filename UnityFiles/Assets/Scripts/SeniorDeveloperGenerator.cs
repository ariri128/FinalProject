using UnityEngine;

public class SeniorDeveloperGenerator : Generator
{
    public SeniorDeveloperGenerator() : base("Senior Developer", 200f, 8f)
    {
    }

    public override float Produce()
    {
        return ownedCount * productionPerUnit;
    }
}
