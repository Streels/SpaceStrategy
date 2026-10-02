using System;
using System.Collections.Generic;

namespace SpaceStrategy.Domain.Economy
{
    public enum StrategicResource { Metals, RareMetals, Chemicals, Superconductors, Minerals, Hypermaterial }
    public enum StoredResource { Fuel, Ammunition }
    public enum ProjectKind { Frigate, Cruiser, Storage, Route }

    public sealed class EconomySettings
    {
        public double TickDays = 0.02;
        public double InitialEfficiency = 0.2;
        public double MaximumEfficiency = 1;
        public double EfficiencyGainPerDay = 0.08;
        public double TnpFraction = 0.25; // Fraction of total PH; autonomous needs are a separate article.
        public double ImportHorizonDays = 2;
        public double CargoDaysPerLeg = 0.5;
        public double FleetDaysPerLeg = 1;
    }

    public sealed class World
    {
        public string Name;
        public bool Controlled = true;
        public bool ExtractionEnabled = true;
        public double TotalPh;
        public double AutonomousPh;
        public readonly double[] Potential = new double[6];
        public readonly double[] Stock = new double[2];
        public readonly double[] StorageCapacity = { 1000, 1000 };
        public readonly double[] Reserve = { 100, 100 };
        public readonly double[] ProductionPerDay = new double[2];
        public readonly double[] ProducedPerDay = new double[2];
        public readonly double[] UsagePerDay = new double[2];
    }

    public sealed class NetworkRoute
    {
        public int A, B;
        public string Name;
        public double Capacity = 60;
        public bool Open = true, Raided;
        public double Demand, Load;
        public readonly double[] CategoryLoad = new double[4]; // strategic, fuel, ammo, fleet
        public double EffectiveCapacity => Open ? Capacity * (Raided ? 0.5 : 1) : 0;
    }

    public sealed class ProjectRecipe
    {
        public string Name;
        public double Work;
        public readonly double[] ResourcesPerPh;
        public ProjectRecipe(string name, double work, params double[] resources)
        { Name = name; Work = work; ResourcesPerPh = resources; }
    }

    public sealed class CapacityPortion
    {
        public double Ph, Efficiency;
    }

    public sealed class ProductionLine
    {
        public readonly int Id;
        public ProjectKind Kind;
        public int Target, Quantity, Completed;
        public double Work, Rate, ResourceFactor = 1;
        public bool Paused;
        public readonly List<CapacityPortion> Portions = new List<CapacityPortion>();
        public readonly double[] Needed = new double[6];
        public readonly double[] Granted = new double[6];
        public ProductionLine(int id) { Id = id; }
        public bool Active => !Paused && Completed < Quantity;
        public double AssignedPh { get { double sum = 0; foreach (var p in Portions) sum += p.Ph; return sum; } }
        public double MasteredPh { get { double sum = 0; foreach (var p in Portions) sum += p.Ph * p.Efficiency; return sum; } }
        public double Efficiency => AssignedPh > 0 ? MasteredPh / AssignedPh : 0;
    }

    public sealed class Cargo
    {
        public int Source, Destination, Leg;
        public StoredResource Resource;
        public double Amount, RemainingDays;
        public int[] Path;
    }

    public sealed class EconomyFleet
    {
        public int Location = 2, Destination = 2, Leg;
        public int[] Path = Array.Empty<int>();
        public bool Travelling, Hyperjump;
        public double Progress, TransportRatio;
        public double Fuel = 60, MaxFuel = 100;
        public double Ammunition = 40, MaxAmmunition = 80;
        public double Organization = 100;
        public bool Exercise;
        public double TransportDemand = 25;
    }

    internal sealed class NetworkFlow
    {
        public int Source, Destination, Category, Resource;
        public int[] Path;
        public double Demand, Granted;
    }

    /// <summary>Standalone v0.3 laboratory. Rates per day. Never uses the legacy hourly economy.</summary>
    public sealed class EconomySimulation
    {
        public static readonly string[] ResourceNames = { "Металлы", "Редкоземельные", "Хим. элементы", "Сверхпроводники", "Минералы", "Гиперматериал" };
        public static readonly ProjectRecipe[] Recipes = {
            new ProjectRecipe("Фрегат", 100, .10, .02, 0, .01, 0, 0),
            new ProjectRecipe("Крейсер", 250, .12, .04, 0, .03, 0, .01),
            new ProjectRecipe("Хранилища +200", 120, .03, 0, 0, 0, .10, 0),
            new ProjectRecipe("Маршрут +20", 160, .03, 0, 0, 0, .07, .015)
        };
        public readonly EconomySettings Settings = new EconomySettings();
        public readonly World[] Worlds;
        public readonly NetworkRoute[] Routes;
        public readonly EconomyFleet Fleet = new EconomyFleet();
        public readonly List<ProductionLine> Lines = new List<ProductionLine>();
        public readonly List<Cargo> Cargoes = new List<Cargo>();
        public readonly double[] Potential = new double[6], Available = new double[6], Used = new double[6];
        public readonly double[] Produced = new double[2], Consumed = new double[2];
        public double Day { get; private set; }
        public int FrigatesBuilt { get; private set; }
        public int CruisersBuilt { get; private set; }
        public double TotalPh { get; private set; }
        public double AutonomousPh { get; private set; }
        public double CivilianPh { get; private set; }
        public double PlayerPh { get; private set; }
        public double AllocationRatio { get; private set; } = 1;
        public double AssignedPh { get { double sum = 0; foreach (var line in Lines) if (line.Active) sum += line.AssignedPh; return sum; } }
        public double FreePh => Math.Max(0, PlayerPh - AssignedPh);
        public string LastEvent { get; private set; } = "Стенд v0.3: меняйте ПЧ, ресурсы и мощности маршрутов.";
        private double remainder;
        private int nextId = 1;
        private readonly List<NetworkFlow> flows = new List<NetworkFlow>();
        private readonly double[,] factoryRatios = new double[3, 2];

        public EconomySimulation()
        {
            Worlds = new[] {
                new World { Name = "Эридан IV", TotalPh = 50, AutonomousPh = 10 },
                new World { Name = "Земля", TotalPh = 200, AutonomousPh = 30 },
                new World { Name = "Бастион", TotalPh = 50, AutonomousPh = 10 }
            };
            double[][] deposits = { new double[] { 12, 3, 1, .5, 8, 2 }, new double[] { 3, 1, 6, 2, 2, .2 }, new double[] { 1, .5, 1, .5, 1, .5 } };
            for (int i = 0; i < Worlds.Length; i++)
            {
                Array.Copy(deposits[i], Worlds[i].Potential, 6);
                Worlds[i].Stock[0] = i == 1 ? 920 : 120;
                Worlds[i].Stock[1] = i == 1 ? 950 : 120;
                Worlds[i].StorageCapacity[0] = Worlds[i].StorageCapacity[1] = i == 1 ? 1000 : 300;
            }
            Worlds[1].ProductionPerDay[0] = 60;
            Worlds[1].ProductionPerDay[1] = 50;
            Worlds[2].UsagePerDay[0] = 12;
            Worlds[2].UsagePerDay[1] = 16;
            Routes = new[] {
                new NetworkRoute { A = 0, B = 1, Name = "Эридан ↔ Земля", Capacity = 80 },
                new NetworkRoute { A = 1, B = 2, Name = "Земля ↔ Бастион", Capacity = 60 }
            };
            AddLine(ProjectKind.Frigate, 2, 50);
            AddLine(ProjectKind.Storage, 1, 30, 2);
            Recalculate();
        }

        public ProductionLine AddLine(ProjectKind kind, int quantity, double ph = 0, int target = 1)
        {
            ValidateKind(kind);
            if (quantity < 1 || target < 0 || target >= Worlds.Length) throw new ArgumentOutOfRangeException();
            var line = new ProductionLine(nextId++) { Kind = kind, Quantity = quantity, Target = target };
            Lines.Add(line);
            Recalculate();
            SetAllocation(line, ph);
            return line;
        }

        public bool SetAllocation(ProductionLine line, double ph)
        {
            if (!Lines.Contains(line) || !Finite(ph) || ph < 0) return false;
            Recalculate();
            double ceiling = line.Active ? line.AssignedPh + FreePh : FreePh;
            if (ph > ceiling + 1e-8) { LastEvent = "Недостаточно свободных ПЧ в имперском пуле."; return false; }
            double old = line.AssignedPh;
            if (ph > old + 1e-8)
                line.Portions.Add(new CapacityPortion { Ph = ph - old, Efficiency = Settings.InitialEfficiency });
            else if (old > 0 && ph < old)
            {
                double ratio = ph / old; // Stand policy: withdraw proportionally from all portions.
                foreach (var portion in line.Portions) portion.Ph *= ratio;
                if (ph == 0) line.Portions.Clear();
            }
            Recalculate();
            return true;
        }

        public void ChangeProduct(ProductionLine line, ProjectKind kind)
        {
            ValidateKind(kind);
            if (!Lines.Contains(line) || line.Kind == kind) return;
            line.Kind = kind;
            line.Work = 0; line.Completed = 0;
            foreach (var portion in line.Portions) portion.Efficiency = Settings.InitialEfficiency;
            LastEvent = "Смена продукции: эффективность сброшена; незавершённая работа сброшена (правило стенда).";
            Recalculate();
        }

        public void MoveLine(ProductionLine line, int offset)
        {
            int old = Lines.IndexOf(line), next = old + offset;
            if (old < 0 || next < 0 || next >= Lines.Count) return;
            Lines.RemoveAt(old); Lines.Insert(next, line);
            Recalculate();
        }

        public void ExtendLine(ProductionLine line)
        { if (Lines.Contains(line)) { line.Quantity++; Recalculate(); } }

        public void Recalculate()
        {
            TotalPh = AutonomousPh = 0;
            foreach (var world in Worlds)
                if (world.Controlled) { TotalPh += Math.Max(0, world.TotalPh); AutonomousPh += Math.Max(0, world.AutonomousPh); }
            CivilianPh = TotalPh * Clamp(Settings.TnpFraction, 0, 1);
            PlayerPh = Math.Max(0, TotalPh - AutonomousPh - CivilianPh);
            AllocationRatio = Ratio(PlayerPh, AssignedPh);
            Array.Clear(Potential, 0, 6); Array.Clear(Available, 0, 6); Array.Clear(Used, 0, 6);
            flows.Clear();
            foreach (var route in Routes) { route.Demand = route.Load = 0; Array.Clear(route.CategoryLoad, 0, 4); }
            for (int w = 0; w < Worlds.Length; w++)
            {
                var world = Worlds[w];
                if (!world.Controlled || !world.ExtractionEnabled) continue;
                int[] path = FindPath(w, 1);
                for (int r = 0; r < 6; r++)
                {
                    double amount = Math.Max(0, world.Potential[r]);
                    Potential[r] += amount;
                    if (path == null) continue;
                    flows.Add(new NetworkFlow { Source = w, Destination = 1, Path = path, Category = 0, Resource = r, Demand = amount });
                }
            }
            PlanShipments();
            if (Fleet.Travelling && !Fleet.Hyperjump)
                flows.Add(new NetworkFlow { Source = Fleet.Path[Fleet.Leg], Destination = Fleet.Path[Fleet.Leg + 1], Path = new[] { Fleet.Path[Fleet.Leg], Fleet.Path[Fleet.Leg + 1] }, Category = 3, Demand = Fleet.TransportDemand });
            AllocateNetwork();
            Fleet.TransportRatio = 0;
            foreach (var flow in flows)
                if (flow.Category == 0) Available[flow.Resource] += flow.Granted;
                else if (flow.Category == 3) Fleet.TransportRatio = Ratio(flow.Granted, flow.Demand);
            var totalNeed = new double[6];
            foreach (var line in Lines)
            {
                for (int r = 0; r < 6; r++)
                {
                    line.Needed[r] = line.Active ? line.AssignedPh * AllocationRatio * Recipes[(int)line.Kind].ResourcesPerPh[r] : 0;
                    totalNeed[r] += line.Needed[r];
                }
            }
            // Local factories occupy chemical capacity, never player-controlled PH.
            for (int w = 0; w < Worlds.Length; w++) for (int r = 0; r < 2; r++)
                if (Worlds[w].Controlled && Worlds[w].Stock[r] < Worlds[w].StorageCapacity[r] - 1e-8)
                    totalNeed[2] += Math.Max(0, Worlds[w].ProductionPerDay[r]) * .01;
            var resourceRatios = new double[6];
            for (int r = 0; r < 6; r++) { resourceRatios[r] = Ratio(Available[r], totalNeed[r]); Used[r] = Math.Min(Available[r], totalNeed[r]); }
            foreach (var line in Lines)
            {
                line.ResourceFactor = 1;
                for (int r = 0; r < 6; r++)
                {
                    line.Granted[r] = line.Needed[r] * resourceRatios[r];
                    if (line.Needed[r] > 0) line.ResourceFactor *= resourceRatios[r];
                }
                line.Rate = line.Active ? line.MasteredPh * AllocationRatio * line.ResourceFactor : 0;
            }
            for (int w = 0; w < Worlds.Length; w++) for (int r = 0; r < 2; r++)
            {
                factoryRatios[w, r] = resourceRatios[2];
                Worlds[w].ProducedPerDay[r] = Worlds[w].Controlled && Worlds[w].Stock[r] < Worlds[w].StorageCapacity[r] - 1e-8
                    ? Math.Max(0, Worlds[w].ProductionPerDay[r]) * factoryRatios[w, r] : 0;
            }
        }

        // Fixed three-world graph; arbitrary/alternative-path optimization is deliberately not chosen.
        public int[] FindPath(int source, int destination)
        {
            if (source < 0 || source >= 3 || destination < 0 || destination >= 3) return null;
            for (int w = Math.Min(source, destination); w <= Math.Max(source, destination); w++)
                if (!Worlds[w].Controlled) return null;
            for (int e = Math.Min(source, destination); e < Math.Max(source, destination); e++)
                if (Routes[e].EffectiveCapacity <= 0) return null;
            var result = new int[Math.Abs(destination - source) + 1];
            int step = Math.Sign(destination - source);
            for (int i = 0; i < result.Length; i++) result[i] = source + i * step;
            return result;
        }

        public bool TransferFleet(int destination, bool hyperjump)
        {
            if (destination < 0 || destination >= 3 || destination == Fleet.Location || Fleet.Travelling || !Worlds[destination].Controlled) return false;
            var path = hyperjump ? new[] { Fleet.Location, destination } : FindPath(Fleet.Location, destination);
            double fuelCost = hyperjump ? 30 : path == null ? double.PositiveInfinity : (path.Length - 1) * 10;
            if (path == null || Fleet.Fuel < fuelCost) { LastEvent = "Переброска недоступна: проверьте маршрут и бортовое топливо."; return false; }
            Fleet.Fuel -= fuelCost; Consumed[0] += fuelCost;
            Fleet.Path = path; Fleet.Destination = destination; Fleet.Travelling = true;
            Fleet.Hyperjump = hyperjump; Fleet.Leg = 0; Fleet.Progress = 0;
            if (hyperjump) Fleet.Organization = Math.Max(0, Fleet.Organization - 20);
            LastEvent = "Флот направлен: " + Worlds[destination].Name;
            Recalculate();
            return true;
        }

        private void PlanShipments()
        {
            double dt = Settings.TickDays;
            double[,] plannedStock = new double[3, 2];
            for (int w = 0; w < 3; w++) for (int r = 0; r < 2; r++) plannedStock[w, r] = Worlds[w].Stock[r];
            foreach (var cargo in Cargoes) plannedStock[cargo.Destination, (int)cargo.Resource] += cargo.Amount;
            for (int r = 0; r < 2; r++)
            {
                var requests = new double[3];
                var surplus = new double[3];
                for (int w = 0; w < 3; w++)
                {
                    if (!Worlds[w].Controlled) continue;
                    double fleetNeed = 0;
                    if (w == Fleet.Location && !Fleet.Travelling)
                        fleetNeed = r == 0 ? Math.Min(6, Math.Max(0, Fleet.MaxFuel - Fleet.Fuel) / Math.Max(dt, Settings.ImportHorizonDays))
                            : Math.Min(8, Math.Max(0, Fleet.MaxAmmunition - Fleet.Ammunition) / Math.Max(dt, Settings.ImportHorizonDays)) + (Fleet.Exercise ? 8 : 0);
                    double localUse = Worlds[w].UsagePerDay[r] + fleetNeed;
                    double target = Math.Max(Worlds[w].Reserve[r], Worlds[w].StorageCapacity[r] * .75);
                    double deficit = Math.Max(0, target - plannedStock[w, r]);
                    requests[w] = Math.Min(Math.Max(0, Worlds[w].StorageCapacity[r] - plannedStock[w, r]) / dt,
                        Math.Max(0, localUse - Worlds[w].ProductionPerDay[r]) + deficit / Math.Max(dt, Settings.ImportHorizonDays));
                    surplus[w] = Math.Max(0, Worlds[w].Stock[r] - target) / dt;
                }
                // Stand distributor: capital hub when needed; local excess may directly serve neighbors.
                for (int source = 0; source < 3; source++)
                {
                    var paths = new int[3][];
                    double need = 0;
                    for (int destination = 0; destination < 3; destination++)
                        if (source != destination && requests[destination] > 0 && (paths[destination] = FindPath(source, destination)) != null) need += requests[destination];
                    double factor = Ratio(surplus[source], need);
                    for (int destination = 0; destination < 3; destination++)
                    {
                        if (paths[destination] == null) continue;
                        double rate = requests[destination] * factor;
                        if (rate <= 0) continue;
                        flows.Add(new NetworkFlow { Source = source, Destination = destination, Category = r + 1, Resource = r, Path = paths[destination], Demand = rate });
                        requests[destination] -= rate;
                    }
                }
            }
        }

        private void AllocateNetwork()
        {
            var remaining = new List<NetworkFlow>();
            foreach (var flow in flows)
            {
                if (flow.Demand <= 0) continue;
                if (flow.Path.Length == 1) { flow.Granted = flow.Demand; continue; }
                remaining.Add(flow);
                for (int leg = 0; leg < flow.Path.Length - 1; leg++) Routes[Math.Min(flow.Path[leg], flow.Path[leg + 1])].Demand += flow.Demand;
            }
            // Progressive proportional filling: a shared bidirectional capacity, never one cap per direction.
            double level = 0;
            while (remaining.Count > 0 && level < 1 - 1e-10)
            {
                double step = 1 - level;
                var needs = new double[Routes.Length];
                foreach (var flow in remaining) for (int leg = 0; leg < flow.Path.Length - 1; leg++) needs[Math.Min(flow.Path[leg], flow.Path[leg + 1])] += flow.Demand;
                for (int e = 0; e < Routes.Length; e++) if (needs[e] > 0) step = Math.Min(step, Math.Max(0, Routes[e].EffectiveCapacity - Routes[e].Load) / needs[e]);
                foreach (var flow in remaining)
                {
                    double addition = step * flow.Demand; flow.Granted += addition;
                    for (int leg = 0; leg < flow.Path.Length - 1; leg++)
                    {
                        var route = Routes[Math.Min(flow.Path[leg], flow.Path[leg + 1])];
                        route.Load += addition; route.CategoryLoad[flow.Category] += addition;
                    }
                }
                level += step;
                remaining.RemoveAll(flow => {
                    for (int leg = 0; leg < flow.Path.Length - 1; leg++)
                    { var route = Routes[Math.Min(flow.Path[leg], flow.Path[leg + 1])]; if (route.Load >= route.EffectiveCapacity - 1e-8) return true; }
                    return false;
                });
                if (step <= 1e-12 && remaining.Count > 0) break;
            }
        }

        public void Advance(double days)
        {
            if (!Finite(days) || days < 0 || !Finite(Settings.TickDays) || Settings.TickDays <= 0) throw new ArgumentOutOfRangeException();
            remainder += days;
            while (remainder + 1e-10 >= Settings.TickDays)
            { Tick(Settings.TickDays); remainder -= Settings.TickDays; }
            if (remainder < 0) remainder = 0;
        }

        private void Tick(double dt)
        {
            AdvanceCargo(dt);
            Recalculate();
            foreach (var flow in flows)
                if (flow.Category == 1 || flow.Category == 2)
                {
                    double amount = Math.Min(Worlds[flow.Source].Stock[flow.Resource], flow.Granted * dt);
                    if (amount <= 0) continue;
                    Worlds[flow.Source].Stock[flow.Resource] -= amount;
                    Cargoes.Add(new Cargo { Source = flow.Source, Destination = flow.Destination, Resource = (StoredResource)flow.Resource,
                        Amount = amount, Path = flow.Path, RemainingDays = Settings.CargoDaysPerLeg });
                }
            for (int w = 0; w < 3; w++) for (int r = 0; r < 2; r++)
            {
                var world = Worlds[w];
                double output = Math.Min(Math.Max(0, world.StorageCapacity[r] - world.Stock[r]), world.ProducedPerDay[r] * dt);
                world.Stock[r] += output; Produced[r] += output;
                double used = Math.Min(world.Stock[r], Math.Max(0, world.UsagePerDay[r]) * dt);
                world.Stock[r] -= used; Consumed[r] += used;
            }
            foreach (var line in Lines)
            {
                if (!line.Active) continue;
                line.Work += line.Rate * dt;
                double cost = Recipes[(int)line.Kind].Work;
                while (line.Work + 1e-9 >= cost && line.Completed < line.Quantity)
                {
                    line.Work = Math.Max(0, line.Work - cost); line.Completed++;
                    Complete(line);
                }
                if (line.Completed == line.Quantity) line.Work = 0;
                // The same sequential resource coefficient slows both output and mastery growth.
                double efficiencyGain = Settings.EfficiencyGainPerDay * line.ResourceFactor * dt;
                foreach (var portion in line.Portions)
                    portion.Efficiency = Math.Min(Settings.MaximumEfficiency, portion.Efficiency + efficiencyGain);
            }
            AdvanceFleet(dt);
            Day += dt;
            Recalculate();
        }

        private void Complete(ProductionLine line)
        {
            if (line.Kind == ProjectKind.Frigate) FrigatesBuilt++;
            else if (line.Kind == ProjectKind.Cruiser) CruisersBuilt++;
            else if (line.Kind == ProjectKind.Storage)
                for (int r = 0; r < 2; r++) Worlds[line.Target].StorageCapacity[r] += 200;
            else Routes[Math.Min(line.Target, Routes.Length - 1)].Capacity += 20;
            LastEvent = Recipes[(int)line.Kind].Name + ": готово " + line.Completed + "/" + line.Quantity + ", линия #" + line.Id;
        }

        private void AdvanceCargo(double dt)
        {
            for (int i = Cargoes.Count - 1; i >= 0; i--)
            {
                var cargo = Cargoes[i];
                int edge = Math.Min(cargo.Path[cargo.Leg], cargo.Path[cargo.Leg + 1]);
                if (Routes[edge].EffectiveCapacity <= 0 || !Worlds[cargo.Path[cargo.Leg + 1]].Controlled) continue;
                cargo.RemainingDays -= dt;
                if (cargo.RemainingDays > 1e-9) continue;
                cargo.Leg++;
                if (cargo.Leg < cargo.Path.Length - 1) { cargo.RemainingDays += Settings.CargoDaysPerLeg; continue; }
                var destination = Worlds[cargo.Destination];
                int resource = (int)cargo.Resource;
                double accepted = Math.Min(cargo.Amount, Math.Max(0, destination.StorageCapacity[resource] - destination.Stock[resource]));
                destination.Stock[resource] += accepted;
                cargo.Amount -= accepted;
                if (cargo.Amount < 1e-8) Cargoes.RemoveAt(i);
                else { cargo.Leg--; cargo.RemainingDays = 0; } // Keep overflow in transit; never discard cargo.
            }
        }

        private void AdvanceFleet(double dt)
        {
            if (!Fleet.Travelling)
            {
                var depot = Worlds[Fleet.Location];
                double fuel = Math.Min(depot.Stock[0], Math.Min(Fleet.MaxFuel - Fleet.Fuel, 6 * dt));
                double ammo = Math.Min(depot.Stock[1], Math.Min(Fleet.MaxAmmunition - Fleet.Ammunition, 8 * dt));
                depot.Stock[0] -= fuel; Fleet.Fuel += fuel;
                depot.Stock[1] -= ammo; Fleet.Ammunition += ammo;
            }
            if (Fleet.Exercise)
            { double ammo = Math.Min(Fleet.Ammunition, 8 * dt); Fleet.Ammunition -= ammo; Consumed[1] += ammo; }
            if (Fleet.Travelling)
            {
                Fleet.Progress += dt * (Fleet.Hyperjump ? 1 / .25 : Fleet.TransportRatio / Settings.FleetDaysPerLeg);
                if (Fleet.Progress >= 1 - 1e-9)
                {
                    Fleet.Location = Fleet.Path[Fleet.Leg + 1]; Fleet.Leg++;
                    if (Fleet.Leg == Fleet.Path.Length - 1) { Fleet.Travelling = false; Fleet.Progress = 0; LastEvent = "Флот прибыл: " + Worlds[Fleet.Location].Name; }
                    else Fleet.Progress = Math.Max(0, Fleet.Progress - 1);
                }
            }
        }

        public double InTransit(int world, int resource)
        { double sum = 0; foreach (var cargo in Cargoes) if (cargo.Destination == world && (int)cargo.Resource == resource) sum += cargo.Amount; return sum; }
        public double PhysicalTotal(int resource)
        { double sum = resource == 0 ? Fleet.Fuel : Fleet.Ammunition; foreach (var world in Worlds) sum += world.Stock[resource]; foreach (var cargo in Cargoes) if ((int)cargo.Resource == resource) sum += cargo.Amount; return sum; }
        public double StorageFill(int resource)
        { double stock = 0, capacity = 0; foreach (var w in Worlds) if (w.Controlled) { stock += w.Stock[resource]; capacity += w.StorageCapacity[resource]; } return capacity > 0 ? stock / capacity : 0; }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void ValidateKind(ProjectKind kind) { if ((int)kind < 0 || (int)kind >= Recipes.Length) throw new ArgumentOutOfRangeException(nameof(kind)); }
        private static double Ratio(double available, double need) => need <= 0 ? 1 : Clamp(available / need, 0, 1);
        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
