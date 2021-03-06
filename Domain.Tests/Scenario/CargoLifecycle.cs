using System;
using System.Collections.Generic;
using System.Linq;

using DomainDrivenDelivery.Domain.Model.Freight;
using DomainDrivenDelivery.Domain.Model.Shared;
using DomainDrivenDelivery.Domain.Model.Travel;
using DomainDrivenDelivery.Domain.Services;
using DomainDrivenDelivery.Infrastructure.Persistence.InMemory;

using NUnit.Framework;

using L = DomainDrivenDelivery.Domain.Model.Locations.SampleLocations;
using V = DomainDrivenDelivery.Domain.Model.Travel.SampleVoyages;

namespace DomainDrivenDelivery.Domain.Tests.Patterns.Scenario
{
    [TestFixture]
    public class CargoLifecycle
    {
        [Test]
        public void cargoIsProperlyDelivered()
        {
            Cargo cargo = setupCargoFromHongkongToStockholm();

            // Hamcrest matchers:
            // assertEquals(1 + 1, 2) <=> Assert.That(1 + 1, Is.EqualTo(2)) <=> Assert.That(1 + 1, equalTo(2))

            // Initial state, before routing
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.NOT_ROUTED));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));
            Assert.IsNull(cargo.NextExpectedActivity);

            // Route: Hongkong - Long Beach - New York - Stockholm
            IEnumerable<Itinerary> itineraries = routingService.fetchRoutesForSpecification(cargo.RouteSpecification);
            Itinerary itinerary = selectAppropriateRoute(itineraries);
            cargo.AssignToRoute(itinerary);

            // Routed
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));

            // Static factory + builder:
            // [HandlingActivity.]HandlingActivity.receiveIn(L.HONGKONG) <=> new HandlingActivity(RECEIVE, L.HONGKONG)
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ReceiveIn(L.HONGKONG)));

            Assert.IsNotNull(cargo.EstimatedTimeOfArrival);
            Assert.That(cargo.CustomsClearancePoint, Is.EqualTo(L.STOCKHOLM));

            // Received
            cargo.Handled(HandlingActivity.ReceiveIn(L.HONGKONG));

            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));

            // Loaded
            cargo.Handled(HandlingActivity.LoadOnto(V.pacific1).In(L.HONGKONG));

            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.pacific1));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH)));
            Assert.False(cargo.IsMisdirected);

            // Unloaded
            cargo.Handled(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH));

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.CurrentVoyage, Is.EqualTo(Voyage.None));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.LoadOnto(V.continental1).In(L.LONGBEACH)));

            cargo.Handled(HandlingActivity.LoadOnto(V.continental1).In(L.LONGBEACH));

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.continental1));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.continental1).In(L.NEWYORK)));

            cargo.Handled(HandlingActivity.UnloadOff(V.continental1).In(L.NEWYORK));

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.NEWYORK));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.CurrentVoyage, Is.EqualTo(Voyage.None));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.LoadOnto(V.atlantic2).In(L.NEWYORK)));

            cargo.Handled(HandlingActivity.LoadOnto(V.atlantic2).In(L.NEWYORK));

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.NEWYORK));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.atlantic2));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.atlantic2).In(L.STOCKHOLM)));

            cargo.Handled(HandlingActivity.UnloadOff(V.atlantic2).In(L.STOCKHOLM));

            Assert.False(cargo.IsReadyToClaim);
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.STOCKHOLM));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.CurrentVoyage, Is.EqualTo(Voyage.None));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.CustomsIn(L.STOCKHOLM)));

            cargo.Handled(HandlingActivity.CustomsIn(L.STOCKHOLM));

            Assert.True(cargo.IsReadyToClaim);
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ClaimIn(L.STOCKHOLM)));

            cargo.Handled(HandlingActivity.ClaimIn(L.STOCKHOLM));

            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.CLAIMED));
            Assert.IsNull(cargo.NextExpectedActivity);
        }

        [Test]
        public void cargoIsUnloadedInWrongLocation()
        {
            Cargo cargo = setupCargoFromHongkongToStockholm();

            // Initial state, before routing
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.NOT_ROUTED));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));
            Assert.IsNull(cargo.NextExpectedActivity);

            // Route: Hongkong - Long Beach - New York - Stockholm
            IEnumerable<Itinerary> itineraries = routingService.fetchRoutesForSpecification(cargo.RouteSpecification);
            Itinerary itinerary = selectAppropriateRoute(itineraries);
            cargo.AssignToRoute(itinerary);

            // Routed
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ReceiveIn(L.HONGKONG)));
            Assert.IsNotNull(cargo.EstimatedTimeOfArrival);

            // Received
            cargo.Handled(HandlingActivity.ReceiveIn(L.HONGKONG));

            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));

            // Loaded
            cargo.Handled(HandlingActivity.LoadOnto(V.pacific1).In(L.HONGKONG));

            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.pacific1));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH)));
            Assert.False(cargo.IsMisdirected);

            // Unloaded in Seattle, wasn't supposed to happen
            cargo.Handled(HandlingActivity.UnloadOff(V.pacific1).In(L.SEATTLE));

            // Misdirected
            Assert.True(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.SEATTLE));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.IsNull(cargo.NextExpectedActivity);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));

            // Reroute: specify new route

            // Assign to new route
            IEnumerable<Itinerary> available =
                routingService.fetchRoutesForSpecification(
                    cargo.RouteSpecification.WithOrigin(cargo.EarliestReroutingLocation));

            Itinerary newItinerary = selectAppropriateRoute(available);
            Itinerary mergedItinerary = cargo.ItineraryMergedWith(newItinerary);
            cargo.AssignToRoute(mergedItinerary);

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.LoadOnto(V.continental3).In(L.SEATTLE)));

            // Loaded, back on track
            cargo.Handled(HandlingActivity.LoadOnto(V.continental3).In(L.SEATTLE));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.SEATTLE));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));

            // Etc
        }

        [Test]
        public void cargoIsLoadedOntoWrongVoyage()
        {
            Cargo cargo = setupCargoFromHongkongToStockholm();

            // Initial state, before routing
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.NOT_ROUTED));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));
            Assert.IsNull(cargo.NextExpectedActivity);

            // Route: Hongkong - Long Beach - New York - Stockholm
            IEnumerable<Itinerary> itineraries = routingService.fetchRoutesForSpecification(cargo.RouteSpecification);
            Itinerary itinerary = selectAppropriateRoute(itineraries);
            cargo.AssignToRoute(itinerary);

            // Routed
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ReceiveIn(L.HONGKONG)));
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.Parse("2009-03-26")));

            // Received
            cargo.Handled(HandlingActivity.ReceiveIn(L.HONGKONG));

            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));

            // Loaded
            cargo.Handled(HandlingActivity.LoadOnto(V.pacific1).In(L.HONGKONG));

            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.pacific1));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH)));
            Assert.False(cargo.IsMisdirected);

            // Unload
            cargo.Handled(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.LoadOnto(V.continental1).In(L.LONGBEACH)));

            // Load onto wrong voyage
            cargo.Handled(HandlingActivity.LoadOnto(V.pacific2).In(L.LONGBEACH));
            Assert.True(cargo.IsMisdirected);
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.IsNull(cargo.NextExpectedActivity);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));

            // Reroute: specify new route

            Assert.That(cargo.EarliestReroutingLocation, Is.EqualTo(L.SEATTLE));

            // Assign to new route
            IEnumerable<Itinerary> available =
                routingService.fetchRoutesForSpecification(
                    cargo.RouteSpecification.WithOrigin(cargo.EarliestReroutingLocation));

            Itinerary newItinerary = selectAppropriateRoute(available);

            Itinerary mergedItinerary = cargo.ItineraryMergedWith(newItinerary);
            cargo.AssignToRoute(mergedItinerary);

            // No longer misdirected
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.UnloadOff(V.pacific2).In(L.SEATTLE)));

            // Loaded
            cargo.Handled(HandlingActivity.UnloadOff(V.pacific2).In(L.SEATTLE));

            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.SEATTLE));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));

            // Etc
        }

        [Test]
        public void customerRequestsChangeOfDestination()
        {
            Cargo cargo = setupCargoFromHongkongToStockholm();

            // Initial state, before routing
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.NOT_ROUTED));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.EstimatedTimeOfArrival, Is.EqualTo(DateTime.MinValue));
            Assert.IsNull(cargo.NextExpectedActivity);

            // Route: Hongkong - Long Beach - New York - Stockholm
            IEnumerable<Itinerary> itineraries = routingService.fetchRoutesForSpecification(cargo.RouteSpecification);
            Itinerary itinerary = selectAppropriateRoute(itineraries);
            cargo.AssignToRoute(itinerary);

            // Routed
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.NOT_RECEIVED));
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ReceiveIn(L.HONGKONG)));
            Assert.IsNotNull(cargo.EstimatedTimeOfArrival);

            // Received
            cargo.Handled(HandlingActivity.ReceiveIn(L.HONGKONG));

            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));

            // Loaded
            cargo.Handled(HandlingActivity.LoadOnto(V.pacific1).In(L.HONGKONG));

            Assert.That(cargo.CurrentVoyage, Is.EqualTo(V.pacific1));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.HONGKONG));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH)));
            Assert.False(cargo.IsMisdirected);

            // Unloaded
            cargo.Handled(HandlingActivity.UnloadOff(V.pacific1).In(L.LONGBEACH));

            Assert.That(cargo.CurrentVoyage, Is.EqualTo(Voyage.None));
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.LoadOnto(V.continental1).In(L.LONGBEACH)));

            // Customer wants cargo to go to Rotterdam instead of Stockholm
            RouteSpecification toRotterdam = cargo.RouteSpecification.WithDestination(L.ROTTERDAM);
            cargo.SpecifyNewRoute(toRotterdam);

            // Misrouted
            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.MISROUTED));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.IN_PORT));

            // Assign to new route
            IEnumerable<Itinerary> available = routingService.fetchRoutesForSpecification(cargo.RouteSpecification);
            Itinerary newItinerary = selectAppropriateRoute(available);
            Itinerary mergedItinerary = cargo.ItineraryMergedWith(newItinerary);

            cargo.AssignToRoute(mergedItinerary);

            Assert.That(cargo.RoutingStatus, Is.EqualTo(RoutingStatus.ROUTED));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.LoadOnto(V.continental2).In(L.LONGBEACH)));

            // Loaded, back on track
            cargo.Handled(HandlingActivity.LoadOnto(V.continental2).In(L.LONGBEACH));
            Assert.False(cargo.IsMisdirected);
            Assert.That(cargo.LastKnownLocation, Is.EqualTo(L.LONGBEACH));
            Assert.That(cargo.TransportStatus, Is.EqualTo(TransportStatus.ONBOARD_CARRIER));
            Assert.That(cargo.NextExpectedActivity,
                Is.EqualTo(HandlingActivity.UnloadOff(V.continental2).In(L.NEWYORK)));

            // Fast forward a bit
            cargo.Handled(HandlingActivity.UnloadOff(V.continental2).In(L.NEWYORK));
            cargo.Handled(HandlingActivity.LoadOnto(V.atlantic1).In(L.NEWYORK));

            // Cargo enters its destination customs zone
            cargo.Handled(HandlingActivity.UnloadOff(V.atlantic1).In(L.ROTTERDAM));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.CustomsIn(L.ROTTERDAM)));
            cargo.Handled(HandlingActivity.CustomsIn(L.ROTTERDAM));
            Assert.That(cargo.NextExpectedActivity, Is.EqualTo(HandlingActivity.ClaimIn(L.ROTTERDAM)));
        }

        // --- Support ---

        private Cargo setupCargoFromHongkongToStockholm()
        {
            TrackingId trackingId = trackingIdFactory.nextTrackingId();
            DateTime arrivalDeadline = DateTime.Parse("2009-04-10");
            RouteSpecification routeSpecification = new RouteSpecification(L.HONGKONG, L.STOCKHOLM, arrivalDeadline);

            return new Cargo(trackingId, routeSpecification);
        }

        private Itinerary selectAppropriateRoute(IEnumerable<Itinerary> itineraries)
        {
            return itineraries.ElementAt(0);
        }

        private readonly RoutingService routingService = new ScenarioStubRoutingService();

        private readonly TrackingIdFactory trackingIdFactory = new TrackingIdFactoryInMem();

        private class ScenarioStubRoutingService : RoutingService
        {
            private static readonly Itinerary itinerary1 =
                new Itinerary(Leg.DeriveLeg(V.pacific1, L.HONGKONG, L.LONGBEACH),
                    Leg.DeriveLeg(V.continental1, L.LONGBEACH, L.NEWYORK),
                    Leg.DeriveLeg(V.atlantic2, L.NEWYORK, L.STOCKHOLM));

            private static readonly Itinerary itinerary2 =
                new Itinerary(Leg.DeriveLeg(V.continental3, L.SEATTLE, L.NEWYORK),
                    Leg.DeriveLeg(V.atlantic2, L.NEWYORK, L.STOCKHOLM));

            private static readonly Itinerary itinerary3 =
                new Itinerary(Leg.DeriveLeg(V.continental2, L.LONGBEACH, L.NEWYORK),
                    Leg.DeriveLeg(V.atlantic1, L.NEWYORK, L.ROTTERDAM));

            public IEnumerable<Itinerary> fetchRoutesForSpecification(RouteSpecification routeSpecification)
            {
                if(routeSpecification.Origin.sameAs(L.HONGKONG) &&
                    routeSpecification.Destination.sameAs(L.STOCKHOLM))
                {
                    // Hongkong - Long Beach - New York - Stockholm, initial routing
                    return new[] {itinerary1};
                }
                else if(routeSpecification.Origin.sameAs(L.SEATTLE) &&
                    routeSpecification.Destination.sameAs(L.STOCKHOLM))
                {
                    // Rotterdam - Hamburg - Stockholm, rerouting misdirected cargo from Rotterdam
                    return new[] {itinerary2};
                }
                else if(routeSpecification.Origin.sameAs(L.HONGKONG) &&
                    routeSpecification.Destination.sameAs(L.ROTTERDAM))
                {
                    // Customer requested change of destination
                    return new[] {itinerary3};
                }
                else
                {
                    throw new InvalidOperationException("No stubbed data for " + routeSpecification);
                }
            }
        }
    }
}