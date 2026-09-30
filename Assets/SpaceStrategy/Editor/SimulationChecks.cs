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
            check(!travel.Transfer(TransferMode.Route), "Closed route must reject normal transfer");
            check(travel.Transfer(TransferMode.Hyperjump), "Hyperjump must work without a route");
            check(travel.Fleet.Fuel == 70 && travel.Fleet.Organization == 65, "Hyperjump must charge fuel and organization");
            travel.Advance(2.1);
            check(!travel.Fleet.Travelling && travel.Fleet.Location == 1, "Hyperjump must arrive after two hours");
            check(travel.QueueShip(), "Earth must accept a shipbuilding order");
            check(!travel.Transfer(TransferMode.Hyperjump), "Fleet must stay docked while its queued ship is built");
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
            return checks + " simulation checks passed";
        }
    }
}
