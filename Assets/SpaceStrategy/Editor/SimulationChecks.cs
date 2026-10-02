using System;
using SpaceStrategy.Domain;

namespace SpaceStrategy.Verification
{
    public static class SimulationChecks
    {
        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new InvalidOperationException(name); checks++; };
            var healthy = new StrategySimulation();
            healthy.Advance(24);
            check(healthy.Fleet.SupplyRatio > 0.99 && healthy.Fleet.Organization > 99, "Healthy network must keep fleet supplied");
            check(healthy.Planets[1].Availability > 0.99, "Healthy raw supply must sustain production");
            var bottleneck = new StrategySimulation();
            bottleneck.Routes[1].Capacity = 15;
            bottleneck.Recalculate();
            check(bottleneck.Fleet.SupplyRatio > 0.99 && bottleneck.Fleet.NetworkSupply < bottleneck.Fleet.Demand, "Reserves must buffer a bottleneck");
            bottleneck.Advance(24);
            check(bottleneck.Fleet.SupplyStock < 0.001 && bottleneck.Fleet.SupplyRatio < 0.5, "Bottleneck must eventually exhaust reserves");
            check(bottleneck.Planets[2].Output < healthy.Planets[2].Output, "Remote planet must lose output after its raw stock is exhausted");
            check(bottleneck.Fleet.Organization < 70, "Sustained deficit must reduce organization");
            check(bottleneck.FleetCause().Contains("перегружен"), "Deficit explanation must identify the route");
            bottleneck.Routes[1].Capacity = 100;
            bottleneck.Advance(12);
            check(bottleneck.Fleet.SupplyRatio > 0.99 && bottleneck.Fleet.SupplyStock > 0, "Restoring capacity must restore supply and refill reserves");
            check(bottleneck.Planets[2].RawStock > 0, "Restoring capacity must also refill planetary raw reserves");
            var outage = new StrategySimulation();
            outage.MiningEnabled = false;
            outage.Advance(24);
            check(outage.Planets[1].Output == 0 && outage.Planets[2].Output == 0, "Stopping mining must eventually stop industrial output");
            var travel = new StrategySimulation();
            travel.Routes[1].Open = false;
            check(!travel.Transfer(1, TransferMode.Route), "Closed route must reject normal transfer");
            check(travel.Transfer(1, TransferMode.Hyperjump), "Hyperjump must work without a route");
            check(travel.Fleet.Fuel == 70 && travel.Fleet.Organization == 65, "Hyperjump must charge fuel and organization");
            travel.Advance(2.1);
            check(!travel.Fleet.Travelling && travel.Fleet.Location == 1, "Hyperjump must arrive after two hours");
            check(travel.QueueShip(), "Earth must accept a shipbuilding order");
            check(!travel.Transfer(0, TransferMode.Hyperjump), "Fleet must stay docked while its queued ship is built");
            travel.Routes[1].Open = true;
            travel.Advance(40);
            check(travel.ShipsBuilt == 1 && travel.Fleet.Ships == 5, "Production must finish exactly one queued frigate");
            check(!new StrategySimulation().QueueShip(), "Fleet must return to the Earth shipyard before adding a ship");
            var steppingA = new StrategySimulation();
            var steppingB = new StrategySimulation();
            steppingA.Routes[1].Capacity = steppingB.Routes[1].Capacity = 20;
            steppingA.Advance(10);
            for(int i = 0; i < 100; i++) steppingB.Advance(0.1);
            check(Math.Abs(steppingA.Fleet.SupplyStock - steppingB.Fleet.SupplyStock) < 0.001, "Simulation must be independent of frame chunking");
            check(steppingA.Fleet.SupplyStock >= 0 && steppingA.Planets[2].RawStock >= 0, "Stocks may never become negative");
            var eridan = new StrategySimulation();
            check(eridan.Transfer(1, TransferMode.Hyperjump), "Fleet can explicitly select Earth");
            eridan.Advance(2.1);
            double oldRawDemand = eridan.Routes[0].Demand;
            check(eridan.Transfer(0, TransferMode.Route), "Earth to Eridan must be available");
            check(eridan.Fleet.ActiveRoute == 0 && eridan.Routes[0].Demand > oldRawDemand + 25, "Eridan trip loads its own route and supply");
            eridan.Advance(12);
            check(eridan.Fleet.Location == 0 && !eridan.Fleet.Travelling, "Fleet arrives at Eridan");
            eridan.Routes[0].Open = false;
            eridan.Recalculate();
            check(eridan.Fleet.NetworkSupply == 0 && eridan.FleetCause().Contains("Эридан"), "Eridan fleet depends on Eridan supply route");
            eridan.Routes[1].Open = false;
            eridan.Routes[0].Open = true;
            check(eridan.Transfer(1, TransferMode.Route), "Closing Bastion route does not block Eridan to Earth");
            eridan.Advance(12);
            check(eridan.Fleet.Location == 1, "Fleet returns from Eridan to Earth");
            eridan.Routes[0].Open = false;
            check(!eridan.Transfer(0, TransferMode.Route), "Closing Eridan route blocks Earth to Eridan");
            check(eridan.Transfer(0, TransferMode.Hyperjump), "Hyperjump reaches Eridan with its route closed");
            check(eridan.Fleet.NetworkSupply == 0, "Hyperjump consumes onboard reserves only");
            eridan.Advance(2.1);
            check(eridan.Fleet.Location == 0 && !eridan.Fleet.Travelling, "Eridan hyperjump completes in two hours");
            var multiLeg = new StrategySimulation();
            multiLeg.Routes[0].Capacity = multiLeg.Routes[1].Capacity = 180;
            check(!multiLeg.Transfer(2, TransferMode.Route) && !multiLeg.Transfer(-1, TransferMode.Route) && !multiLeg.Transfer(3, TransferMode.Route), "Reject current and invalid destinations");
            check(multiLeg.Fleet.Fuel == 100, "Rejected orders do not consume fuel");
            check(multiLeg.Transfer(0, TransferMode.Route) && multiLeg.Fleet.Fuel == 80, "Two-leg trip costs twenty fuel");
            multiLeg.Advance(6.1);
            check(multiLeg.Fleet.Travelling && multiLeg.Fleet.Location == 1 && multiLeg.Fleet.ActiveRoute == 0, "Two-leg trip passes Earth and changes active route");
            multiLeg.Routes[0].Open = false;
            double progress = multiLeg.Fleet.TravelProgress;
            multiLeg.Advance(1);
            check(Math.Abs(multiLeg.Fleet.TravelProgress - progress) < 0.000001, "Closing active route stops travel progress");
            multiLeg.Routes[0].Open = true;
            multiLeg.Advance(6.1);
            check(multiLeg.Fleet.Location == 0 && !multiLeg.Fleet.Travelling, "Two-leg trip reaches final destination");
            var fuel = new StrategySimulation();
            fuel.Fleet.Fuel = 15;
            check(!fuel.Transfer(0, TransferMode.Route) && fuel.Fleet.Fuel == 15, "Check fuel for all legs before departure");
            return checks + " simulation checks passed";
        }
    }
}
