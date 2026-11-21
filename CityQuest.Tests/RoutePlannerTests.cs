using System.ComponentModel.DataAnnotations;
using CityQuest.Application.Exceptions;
using CityQuest.Application.Routes;
using CityQuest.Domain.Entities;
using Xunit;

namespace CityQuest.Tests;

public class RoutePlannerTests
{
    private readonly RoutePlanner planner = new();

    private static RoutePlanRequest Request() => new()
    {
        StartLatitude = 50.74, StartLongitude = 25.32,
        Interests = ["architecture", "nature"], TimeBudgetMinutes = 120
    };

    private static Place Place(string category, double latitude = 50.74, int visitMinutes = 10) => new()
    {
        Name = category, Category = category, Latitude = latitude, Longitude = 25.32,
        VisitMinutes = visitMinutes
    };

    [Fact]
    public void FoodIsExcludedUnlessRequested()
    {
        var plan = planner.Plan(Request(), [Place("architecture"), Place("nature"), Place("cafe")]);
        Assert.Equal(2, plan.Stops.Count);
        Assert.DoesNotContain(plan.Stops, x => x.Category == "cafe");
        Assert.True(plan.IsEstimated);
        Assert.NotEmpty(plan.Warnings);
    }

    [Theory]
    [InlineData("cafe")]
    [InlineData("restaurant")]
    public void FoodPreferenceAddsExactlyOneMatchingStop(string type)
    {
        var request = Request();
        request.IncludeFood = true;
        request.FoodType = type;
        var plan = planner.Plan(request, [Place("architecture"), Place("cafe"), Place("restaurant"), Place(type)]);
        Assert.Single(plan.Stops, x => x.Category == type);
        Assert.Equal(2, plan.Stops.Count);
        Assert.True(plan.TotalMinutes <= request.TimeBudgetMinutes);
        Assert.Equal(plan.Stops.Sum(x => x.VisitMinutes + x.WalkingMinutes), plan.TotalMinutes);
        Assert.Equal(plan.Stops.Count, plan.Stops.Select(x => x.PlaceId).Distinct().Count());
    }

    [Fact]
    public void MissingFoodOrImpossibleBudgetDoesNotSilentlyDropFood()
    {
        var request = Request();
        request.IncludeFood = true;
        Assert.Throws<RoutePlanningException>(() => planner.Plan(request, [Place("architecture")]));
        request.TimeBudgetMinutes = 15;
        Assert.Throws<RoutePlanningException>(() => planner.Plan(request, [Place("architecture"), Place("cafe", visitMinutes: 30)]));
    }

    [Fact]
    public void ReturnWalkIsIncludedInBudgetAndTotals()
    {
        var request = Request();
        request.TimeBudgetMinutes = 20;
        Place[] places = [Place("architecture", 50.744)];
        var open = planner.Plan(request, places);
        Assert.True(open.TotalMinutes <= 20);
        request.ReturnToStart = true;
        Assert.Throws<RoutePlanningException>(() => planner.Plan(request, places));
        request.TimeBudgetMinutes = 40;
        var loop = planner.Plan(request, places);
        Assert.True(loop.ReturnDistanceMeters > 0);
        Assert.Equal(open.TotalMinutes + loop.ReturnWalkingMinutes, loop.TotalMinutes);
        Assert.Equal(loop.Stops.Sum(x => x.DistanceMeters) + loop.ReturnDistanceMeters, loop.DistanceMeters);
    }

    [Fact]
    public void SelectedCategoriesArePreferredAndStopLimitIsRespected()
    {
        var request = Request();
        request.MaxStops = 2;
        var plan = planner.Plan(request,
            [Place("architecture"), Place("architecture", 50.7401), Place("nature", 50.741), Place("museum")]);
        Assert.Equal(2, plan.Stops.Count);
        Assert.Contains(plan.Stops, x => x.Category == "nature");
        Assert.DoesNotContain(plan.Stops, x => x.Category == "museum");
        Assert.Equal(new[] { 1, 2 }, plan.Stops.Select(x => x.Position));
    }

    [Fact]
    public void EmptyOrDistantCatalogCannotProduceARoute()
    {
        Assert.Throws<RoutePlanningException>(() => planner.Plan(Request(), []));
        Assert.Throws<RoutePlanningException>(() => planner.Plan(Request(), [Place("architecture", 52)]));
    }

    [Fact]
    public void InvalidPreferencesAreRejected()
    {
        var request = Request();
        request.Interests = ["unknown"];
        Assert.Throws<ValidationException>(() => planner.Plan(request, []));
        request.Interests = ["nature"];
        request.FoodType = "cafe";
        Assert.Throws<ValidationException>(() => planner.Plan(request, []));
        request.IncludeFood = true;
        request.MaxStops = 1;
        Assert.Throws<ValidationException>(() => planner.Plan(request, []));
    }

    [Fact]
    public void PlannerIsDeterministicAndDoesNotMutatePlaces()
    {
        Place[] places = [Place("nature"), Place("architecture")];
        var first = planner.Plan(Request(), places);
        var second = planner.Plan(Request(), places.Reverse().ToList());
        Assert.Equal(first.Stops, second.Stops);
        Assert.Equal("nature", places[0].Name);
    }
}
