namespace CityQuest.Domain;

public static class PlaceCategories
{
    public static readonly string[] Attractions = ["architecture", "nature", "monument", "museum"];
    public static readonly string[] Food = ["cafe", "restaurant"];
    public static bool IsFood(string category) => Food.Contains(category);
}
