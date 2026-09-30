using System;

namespace SpaceStrategy.Domain
{
    public enum TransferMode { Route, Hyperjump }

    public sealed class PlanetState
    {
        public string Name;
        public string Role;
        public double Capacity;
        public double Efficiency;
        public double Tempo = 0.8;
        public double RawStock;
        public double MaxRawStock;
        public double RawDelivered;
        public double RawNeeded;
        public double Output;
        public double Availability;
        public double PotentialOutput => Capacity * Efficiency * Tempo;
    }

    public sealed class RouteState
    {
        public string Name;
        public double Capacity;
        public bool Open = true;
        public bool Raided;
        public double Demand;
        public double EffectiveCapacity => Open ? Capacity * (Raided ? 0.5 : 1.0) : 0;
        public double DeliveryRatio => Demand <= 0 ? 1 : Math.Min(1, EffectiveCapacity / Demand);
        public double Load => Math.Min(Demand, EffectiveCapacity);
        public bool Overloaded => Demand > EffectiveCapacity + 0.001;
    }

    public sealed class FleetState
    {
        public int Ships = 4;
        public int Location = 2;
        public int Origin = 2;
        public int Destination = 2;
        public bool Travelling;
        public TransferMode Mode;
        public double TravelProgress;
        public double Organization = 100;
        public double Fuel = 100;
        public double SupplyStock = 120;
        public double MaxSupplyStock = 150;
        public double NetworkSupply;
        public double SupplyRatio = 1;
        public double Demand => Ships * 6.0;
    }

    /// <summary>All rates are per game hour; Unity and the UI do not own economic rules.</summary>
    public sealed class StrategySimulation
    {
        public readonly PlanetState[] Planets = {
            new PlanetState { Name = "Эридан IV", Role = "Добывающий мир", Capacity = 92, Efficiency = 1, Tempo = 1 },
            new PlanetState { Name = "Земля", Role = "Промышленный центр", Capacity = 120, Efficiency = 0.85, RawStock = 180, MaxRawStock = 240 },
            new PlanetState { Name = "Бастион", Role = "Передовая база", Capacity = 40, Efficiency = 0.65, Tempo = 0.7, RawStock = 60, MaxRawStock = 80 }
        };
        public readonly RouteState[] Routes = {
            new RouteState { Name = "Эридан IV → Земля", Capacity = 100 },
            new RouteState { Name = "Земля → Бастион", Capacity = 70 }
        };
        public readonly FleetState Fleet = new FleetState();
        public const double ShipCost = 2000;
        public double Hour { get; private set; }
        public bool MiningEnabled = true;
        public bool IndustrialPriority;
        public bool ShipQueued { get; private set; }
        public double ShipWork { get; private set; }
        public int ShipsBuilt { get; private set; }
        public string LastEvent { get; private set; } = "Сеть стабильна. Попробуйте ограничить маршрут к Бастиону.";
        public double MiningOutput => MiningEnabled ? Planets[0].PotentialOutput : 0;
        public double TotalOutput => Planets[1].Output + Planets[2].Output;

        public StrategySimulation() { Recalculate(); }

        public void Recalculate()
        {
            Planets[1].Capacity = IndustrialPriority ? 150 : 120;
            Planets[1].RawNeeded = Planets[1].PotentialOutput * 0.7;
            Planets[2].RawNeeded = Planets[2].PotentialOutput * 0.7;
            double earthOrder = Planets[1].RawNeeded * (Planets[1].RawStock < Planets[1].MaxRawStock - 0.001 ? 1.1 : 1);
            double bastionOrder = Planets[2].RawNeeded * (Planets[2].RawStock < Planets[2].MaxRawStock - 0.001 ? 1.1 : 1);
            double rawDemand = earthOrder + bastionOrder;
            double extractionRatio = rawDemand > 0 ? Math.Min(1, MiningOutput / rawDemand) : 1;
            Routes[0].Demand = rawDemand + 20;
            bool remoteFleet = Fleet.Travelling || Fleet.Location == 2;
            double supplyOrder = Fleet.Demand * (Fleet.SupplyStock < Fleet.MaxSupplyStock - 0.001 ? 1.15 : 1);
            Routes[1].Demand = bastionOrder + 12 + (remoteFleet ? supplyOrder : 0)
                + (Fleet.Travelling && Fleet.Mode == TransferMode.Route ? 25 : 0);
            Planets[1].RawDelivered = earthOrder * extractionRatio * Routes[0].DeliveryRatio;
            Planets[2].RawDelivered = bastionOrder * extractionRatio * Routes[0].DeliveryRatio * Routes[1].DeliveryRatio;
            for (int i = 1; i < 3; i++)
            {
                PlanetState p = Planets[i];
                p.Availability = p.RawStock > 0.001 ? 1 : Ratio(p.RawDelivered, p.RawNeeded);
                p.Output = p.PotentialOutput * p.Availability;
            }
            // Spare output above fleet upkeep fills the depot; shipbuilding uses the remainder.
            double supplyProductionRatio = Math.Min(1, Planets[1].Output / (Fleet.Demand * 0.7));
            Fleet.NetworkSupply = supplyOrder * supplyProductionRatio * (remoteFleet ? Routes[1].DeliveryRatio : 1);
            Fleet.SupplyRatio = Fleet.SupplyStock > 0.001 ? 1 : Ratio(Fleet.NetworkSupply, Fleet.Demand);
        }

        public void Advance(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
                throw new ArgumentOutOfRangeException(nameof(hours));
            while (hours > 0.000001)
            {
                double dt = Math.Min(0.1, hours);
                Tick(dt);
                hours -= dt;
            }
        }

        private void Tick(double dt)
        {
            Recalculate();
            double earthActual = 0;
            for (int i = 1; i < 3; i++)
            {
                PlanetState p = Planets[i];
                double need = p.RawNeeded * dt;
                double consumed = Math.Min(need, p.RawStock + p.RawDelivered * dt);
                p.RawStock = Clamp(p.RawStock + p.RawDelivered * dt - consumed, 0, p.MaxRawStock);
                if (i == 1) earthActual = p.PotentialOutput * Ratio(consumed, need);
            }
            double supplyNeed = Fleet.Demand * dt;
            double supplied = Math.Min(supplyNeed, Fleet.SupplyStock + Fleet.NetworkSupply * dt);
            Fleet.SupplyStock = Clamp(Fleet.SupplyStock + Fleet.NetworkSupply * dt - supplied, 0, Fleet.MaxSupplyStock);
            double effectiveSupply = Ratio(supplied, supplyNeed);
            Fleet.Organization = Clamp(Fleet.Organization + (effectiveSupply >= 0.9 ? 1.5 : -8 * (1 - effectiveSupply)) * dt, 0, 100);
            if (!Fleet.Travelling && effectiveSupply >= 0.9)
                Fleet.Fuel = Math.Min(100, Fleet.Fuel + 2 * dt);
            if (ShipQueued)
            {
                double upkeep = Fleet.Demand * 0.7;
                ShipWork += Math.Max(0, earthActual - upkeep) * dt;
                if (ShipWork >= ShipCost)
                {
                    ShipWork = ShipCost;
                    ShipQueued = false;
                    ShipsBuilt++;
                    Fleet.Ships++;
                    LastEvent = "Фрегат построен и присоединён к флоту. Потребность в снабжении выросла.";
                }
            }
            if (Fleet.Travelling)
            {
                double speed = Fleet.Mode == TransferMode.Hyperjump ? 1.0 / 2 : Routes[1].DeliveryRatio / 6;
                Fleet.TravelProgress = Math.Min(1, Fleet.TravelProgress + speed * dt);
                if (Fleet.TravelProgress >= 1)
                {
                    Fleet.Location = Fleet.Destination;
                    Fleet.Travelling = false;
                    LastEvent = "Флот прибыл: " + Planets[Fleet.Location].Name + ".";
                }
            }
            Hour += dt;
            Recalculate();
        }

        public bool QueueShip()
        {
            if (ShipQueued || Fleet.Travelling || Fleet.Location != 1) return false;
            ShipQueued = true;
            ShipWork = 0;
            LastEvent = "Верфь начала сборку фрегата. После расходов на снабжение весь свободный выпуск идёт в заказ.";
            return true;
        }

        public bool Transfer(TransferMode mode)
        {
            double fuelCost = mode == TransferMode.Hyperjump ? 30 : 10;
            if (ShipQueued || Fleet.Travelling || Fleet.Fuel < fuelCost || (mode == TransferMode.Route && !Routes[1].Open)) return false;
            Fleet.Origin = Fleet.Location;
            Fleet.Destination = Fleet.Location == 1 ? 2 : 1;
            Fleet.Mode = mode;
            Fleet.TravelProgress = 0;
            Fleet.Travelling = true;
            Fleet.Fuel -= fuelCost;
            if (mode == TransferMode.Hyperjump) Fleet.Organization = Math.Max(0, Fleet.Organization - 35);
            LastEvent = mode == TransferMode.Hyperjump ? "Гиперпрыжок: 2 часа, −30 топлива, −35 организации." : "Переброска: от 6 часов, −10 топлива. Флот занимает 25 единиц маршрута.";
            Recalculate();
            return true;
        }

        public string FleetCause()
        {
            if (Fleet.NetworkSupply >= Fleet.Demand) return "Поставки покрывают расход. Резерв пополняется.";
            string cause = "Недостаточно промышленного выпуска Земли.";
            if ((Fleet.Travelling || Fleet.Location == 2) && Routes[1].Overloaded)
                cause = !Routes[1].Open ? "Земля → Бастион закрыт." : Routes[1].Raided ? "Рейд снижает мощность Земля → Бастион вдвое." : "Земля → Бастион перегружен.";
            double deficit = Fleet.Demand - Fleet.NetworkSupply;
            return cause + (Fleet.SupplyStock > 0 ? " Резерва хватит на " + (Fleet.SupplyStock / deficit).ToString("0.0") + " ч." : " Резерв исчерпан; организация падает.");
        }

        public string PlanetCause(int index)
        {
            if (index == 0) return MiningEnabled ? "Добыча покрывает потребности промышленности." : "Добыча остановлена. Промышленность расходует запасы.";
            PlanetState p = Planets[index];
            if (p.RawDelivered >= p.RawNeeded - 0.001) return "Сырья достаточно. Промышленность обеспечена.";
            string cause = !MiningEnabled ? "Эридан IV не поставляет сырьё." : Routes[0].Overloaded ? "Эридан IV → Земля перегружен." : MiningOutput < Planets[1].RawNeeded + Planets[2].RawNeeded ? "Добычи Эридана IV недостаточно." : "Земля → Бастион перегружен.";
            double deficit = p.RawNeeded - p.RawDelivered;
            return cause + (p.RawStock > 0 ? " Запаса хватит на " + (p.RawStock / deficit).ToString("0.0") + " ч." : " Запас исчерпан; выпуск снижен.");
        }

        private static double Ratio(double value, double need) => need <= 0 ? 1 : Math.Min(1, Math.Max(0, value / need));
        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
