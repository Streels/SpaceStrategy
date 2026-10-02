using System;

namespace SpaceStrategy.Domain.Economy
{
    public static class EconomyChecks
    {
        private static EconomySimulation Isolated()
        {
            var s = new EconomySimulation(); s.Lines.Clear();
            for (int w = 0; w < 3; w++)
            {
                s.Worlds[w].ExtractionEnabled = w == 1;
                for (int r = 0; r < 2; r++)
                { s.Worlds[w].ProductionPerDay[r] = s.Worlds[w].UsagePerDay[r] = 0; s.Worlds[w].Stock[r] = s.Worlds[w].StorageCapacity[r]; }
                for (int r = 0; r < 6; r++) s.Worlds[w].Potential[r] = w == 1 ? 1000 : 0;
            }
            s.Settings.InitialEfficiency = 1; s.Recalculate();
            return s;
        }

        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new InvalidOperationException("Economy: " + name); checks++; };
            Func<double, double, bool> near = (a, b) => Math.Abs(a - b) < 1e-7;
            var s = Isolated();
            check(near(s.TotalPh, 300) && near(s.AutonomousPh, 50) && near(s.CivilianPh, 75) && near(s.PlayerPh, 175), "separate autonomous, civilian and player PH articles");
            var line = s.AddLine(ProjectKind.Frigate, 2, 100);
            check(near(line.Rate, 100), "fully efficient line with sufficient resources");
            check(near(line.Needed[0], 10), "resource demand scales with nominal PH");
            s.Worlds[1].Potential[0] = 9; s.Recalculate();
            check(near(line.ResourceFactor, .9) && near(line.Rate, 90), "9 of 10 means 90 percent rate");
            s.Worlds[1].Potential[0] = 8; s.Worlds[1].Potential[3] = .5; s.Recalculate();
            check(near(line.ResourceFactor, .4) && near(line.Rate, 40), "20 percent and 50 percent penalties sequentially give 40 percent, not 30 or 50");
            s.Worlds[1].Potential[0] = 0; s.Recalculate();
            check(near(line.Rate, 0), "proportional coefficient at full deficit");
            s.Worlds[1].Potential[0] = 1000; s.Worlds[1].Potential[3] = 1000; s.Recalculate();
            check(near(line.Rate, 100), "surplus does not boost resource coefficient above one");
            check(!s.SetAllocation(line, 176) && near(line.AssignedPh, 100), "allocation cannot exceed empire pool");
            check(!s.SetAllocation(line, -1) && !s.SetAllocation(line, double.NaN), "reject negative and nonfinite allocations");
            s.SetAllocation(line, 50);
            check(near(line.Needed[0], 5), "halving allocated PH halves resource demand");

            s = Isolated(); s.Settings.InitialEfficiency = .2;
            line = s.AddLine(ProjectKind.Frigate, 10, 50);
            s.Advance(5);
            check(line.Efficiency > .59 && line.Efficiency < .61, "efficiency accumulates over time");
            double mastered = line.MasteredPh, efficiency = line.Efficiency;
            s.SetAllocation(line, 100);
            check(near(line.MasteredPh, mastered + 50 * .2), "only added PH starts at base efficiency");
            check(line.Portions[0].Efficiency == efficiency, "expanding a line preserves existing efficiency");
            double before = line.Efficiency;
            s.SetAllocation(line, 50);
            check(near(line.Efficiency, before), "partial proportional withdrawal preserves efficiency of remaining portions");
            var other = s.AddLine(ProjectKind.Frigate, 10, 0);
            s.SetAllocation(line, 25); s.SetAllocation(other, 25);
            check(near(other.Efficiency, .2), "transferred PH cannot transfer efficiency even for identical product");
            before = line.Efficiency;
            s.MoveLine(line, 1);
            check(near(line.Efficiency, before) && near(line.AssignedPh, 25), "reordering does not change PH or efficiency");
            s.ChangeProduct(line, ProjectKind.Cruiser);
            check(near(line.Efficiency, .2) && near(line.Work, 0), "changing product resets to base efficiency");

            foreach (double factor in new[] { 0.0, .4, .9, 1.0 })
            {
                s = Isolated(); s.Settings.InitialEfficiency = .2;
                line = s.AddLine(ProjectKind.Frigate, 1000, 100);
                s.Worlds[1].Potential[0] = factor == .4 ? 8 : 10 * factor;
                if (factor == .4) s.Worlds[1].Potential[3] = .5;
                s.Recalculate();
                check(near(line.ResourceFactor, factor), "resource coefficient for efficiency scenario");
                s.Advance(5);
                check(near(line.Efficiency, .2 + .08 * 5 * factor), "efficiency growth scales with the combined resource coefficient");
                if (factor == 0)
                {
                    check(near(line.Work, 0) && near(line.AssignedPh, 100), "full shortage preserves assignment and stops work and mastery");
                    s.Worlds[1].Potential[0] = 1000; s.Recalculate();
                    check(near(line.Efficiency, .2) && near(line.Rate, 20), "resource recovery resumes from preserved efficiency without instant mastery");
                    s.Advance(1);
                    check(near(line.Efficiency, .28), "mastery growth resumes after resource recovery");
                }
            }
            s = Isolated(); s.Settings.InitialEfficiency = .99;
            line = s.AddLine(ProjectKind.Frigate, 1000, 100);
            s.Worlds[1].Potential[0] = 8; s.Worlds[1].Potential[3] = .5; s.Advance(1);
            check(near(line.Efficiency, 1), "resource-scaled mastery respects the maximum efficiency");

            s = Isolated(); line = s.AddLine(ProjectKind.Frigate, 2, 100);
            s.Advance(1);
            check(line.Completed == 1 && near(line.Efficiency, 1) && line.Active, "first unit of series completes without resetting efficiency");
            s.Advance(1);
            check(line.Completed == 2 && s.FrigatesBuilt == 2 && !line.Active, "exactly two units finish");
            check(near(s.FreePh, s.PlayerPh), "completed series releases active PH pool");
            s.Advance(2);
            check(s.FrigatesBuilt == 2, "completed line cannot continue producing without extension");
            s = Isolated();
            var a = s.AddLine(ProjectKind.Frigate, 3, 50); var b = s.AddLine(ProjectKind.Frigate, 3, 50);
            a.Portions[0].Efficiency = .8; b.Portions[0].Efficiency = .2; s.Recalculate();
            check(near(a.Rate, 40) && near(b.Rate, 10), "identical independent lines keep different efficiency");
            s.Worlds[1].Potential[0] = 5; s.Recalculate();
            check(near(a.Granted[0], 2.5) && near(b.Granted[0], 2.5), "same material capacity is not double-allocated");
            check(near(s.Used[0], 5), "global used material capacity never exceeds available");
            s.Settings.TnpFraction = .9; s.Recalculate();
            check(near(s.PlayerPh, 0) && near(a.Rate, 0) && near(b.Rate, 0), "civilian and autonomous costs cannot produce negative PH");

            s = new EconomySimulation();
            s.Routes[0].Open = false; s.Recalculate();
            check(s.Potential[0] > s.Available[0], "isolated strategic source remains potential, not available");
            double unavailable = s.Available[0];
            s.Routes[0].Open = true; s.Routes[0].Capacity = 10000; s.Routes[1].Capacity = 10000; s.Recalculate();
            check(s.Available[0] > unavailable && near(s.Available[0], s.Potential[0]), "restored network reconnects source without stored raw goods");
            s.Routes[0].Capacity = 10; s.Recalculate();
            check(s.Routes[0].Demand > 10 && s.Routes[0].Load <= 10 + 1e-8, "overload has a bounded shared capacity");
            check(s.Available[0] > 0, "partial overload does not turn off entire economy");
            for (int e = 0; e < 2; e++)
            { double categories = 0; foreach (double v in s.Routes[e].CategoryLoad) categories += v; check(near(categories, s.Routes[e].Load), "route categories have one conserved shared budget"); }
            s.Worlds[0].Controlled = false; s.Recalculate();
            check(s.FindPath(0, 1) == null, "uncontrolled world cannot contribute through route");

            s = new EconomySimulation();
            double fuel = s.PhysicalTotal(0), ammo = s.PhysicalTotal(1);
            s.Advance(2);
            check(near(s.PhysicalTotal(0), fuel + s.Produced[0] - s.Consumed[0]), "fuel mass conservation across stocks, fleet and cargo");
            check(near(s.PhysicalTotal(1), ammo + s.Produced[1] - s.Consumed[1]), "ammo mass conservation across stocks, fleet and cargo");
            s.Fleet.Exercise = true;
            s.Routes[1].Open = false; s.Advance(30);
            check(near(s.PhysicalTotal(0), fuel + s.Produced[0] - s.Consumed[0]), "fuel conserved during long blockade");
            check(near(s.PhysicalTotal(1), ammo + s.Produced[1] - s.Consumed[1]), "ammo conserved during blockade and exercises");
            check(s.Worlds[2].Stock[0] >= 0 && s.Worlds[2].Stock[1] >= 0 && s.Fleet.Ammunition >= 0, "depot and fleet stocks never negative");
            check(s.Worlds[2].Stock[1] < 1e-7, "isolated front eventually exhausts ammunition reserve");
            s.Routes[1].Open = true; s.Advance(4);
            check(s.Worlds[2].Stock[0] > 0, "restored shipments actually refill local fuel stock");
            foreach (var world in s.Worlds) for (int r = 0; r < 2; r++) check(world.Stock[r] <= world.StorageCapacity[r] + 1e-7, "stock respects storage capacity");
            s = Isolated();
            s.Worlds[0].Stock[0] = 0;
            s.Cargoes.Add(new Cargo { Source = 1, Destination = 0, Resource = StoredResource.Fuel, Amount = 10, Path = new[] { 1, 0 }, RemainingDays = .5 });
            s.Worlds[1].Stock[0] -= 10;
            double total = s.PhysicalTotal(0);
            s.Routes[0].Open = false; s.Advance(1);
            check(s.Cargoes.Exists(c => c.Amount == 10) && near(s.PhysicalTotal(0), total), "closed route retains cargo without disappearance");
            s.Routes[0].Open = true; s.Advance(.5);
            check(s.Worlds[0].Stock[0] >= 10, "existing cargo arrives after reopening");
            s = Isolated();
            s.Cargoes.Add(new Cargo { Source = 1, Destination = 0, Resource = StoredResource.Fuel, Amount = 10, Path = new[] { 1, 0 }, RemainingDays = .02 });
            s.Worlds[1].Stock[0] -= 10; total = s.PhysicalTotal(0); s.Advance(.02);
            check(near(s.PhysicalTotal(0), total) && s.Cargoes.Count > 0, "full destination keeps overflow cargo, not deletion");

            s = Isolated(); s.Routes[0].Capacity = s.Routes[1].Capacity = 10000;
            check(!s.TransferFleet(2, false) && !s.TransferFleet(-1, false), "reject current and invalid fleet targets");
            check(s.TransferFleet(1, false), "Bastion to Earth allowed"); s.Advance(1);
            check(!s.Fleet.Travelling && s.Fleet.Location == 1, "fleet reaches Earth");
            check(s.TransferFleet(0, false), "Earth to Eridan allowed in new stand"); s.Advance(1);
            check(s.Fleet.Location == 0 && !s.Fleet.Travelling, "fleet reaches Eridan");
            s.Routes[0].Open = false; double oldFuel = s.Fleet.Fuel;
            check(!s.TransferFleet(1, false) && near(s.Fleet.Fuel, oldFuel), "rejected transfer cannot charge fuel");
            check(s.TransferFleet(1, true), "hyperjump can bypass blocked route"); s.Advance(.26);
            check(s.Fleet.Location == 1 && !s.Fleet.Travelling, "hyperjump completes");
            s = Isolated(); s.Routes[0].Capacity = s.Routes[1].Capacity = 10000;
            check(s.TransferFleet(0, false), "multi-leg transfer accepted"); s.Advance(1);
            check(s.Fleet.Travelling && s.Fleet.Location == 1 && s.Fleet.Leg == 1, "multi-leg transfer changes active segment at Earth");
            s.Routes[0].Open = false; double oldProgress = s.Fleet.Progress; s.Advance(.4);
            check(near(oldProgress, s.Fleet.Progress), "closing active segment stops transfer");
            s.Routes[0].Open = true; s.Advance(1);
            check(s.Fleet.Location == 0 && !s.Fleet.Travelling, "reopening resumes same transfer");

            s = Isolated(); s.Routes[0].Open = s.Routes[1].Open = false;
            s.Worlds[2].Stock[0] = s.Worlds[2].Stock[1] = 50;
            fuel = s.PhysicalTotal(0); ammo = s.PhysicalTotal(1);
            s.Advance(.5);
            check(near(s.Worlds[2].Stock[0], 47) && near(s.Fleet.Fuel, 63), "orbiting fleet draws fuel below the export reserve during blockade");
            check(near(s.Worlds[2].Stock[1], 46) && near(s.Fleet.Ammunition, 44), "orbiting fleet draws ammunition below the export reserve during blockade");
            check(near(s.PhysicalTotal(0), fuel) && near(s.PhysicalTotal(1), ammo), "orbital refill transfers stock without creating or consuming it");
            check(s.Cargoes.Count == 0, "isolated reserve is not exported");

            s = Isolated(); s.Routes[0].Capacity = s.Routes[1].Capacity = 10000;
            s.Fleet.Exercise = true;
            check(s.TransferFleet(0, false), "reserve scenario starts a two-leg fleet journey");
            s.Routes[1].Open = false;
            fuel = s.PhysicalTotal(0); ammo = s.PhysicalTotal(1);
            s.Advance(.5);
            check(s.Fleet.Travelling && near(s.Fleet.Progress, 0), "fleet stopped on closed route remains in transit, not in orbit");
            check(near(s.Fleet.Fuel, 40) && near(s.Fleet.Ammunition, 36), "blocked travelling fleet uses only onboard stores");
            check(near(s.Worlds[2].Stock[0], 300) && near(s.Worlds[2].Stock[1], 300), "departed fleet cannot drain the origin depot remotely");
            s.Routes[1].Open = true; s.Routes[0].Open = false; s.Advance(1);
            check(s.Fleet.Travelling && s.Fleet.Location == 1 && s.Fleet.Leg == 1, "intermediate waypoint does not end a fleet journey");
            double waypointAmmo = s.Fleet.Ammunition; s.Advance(.5);
            check(near(s.Fleet.Fuel, 40) && near(s.Fleet.Ammunition, waypointAmmo - 4), "waiting at intermediate waypoint cannot refill from its depot");
            check(near(s.Worlds[1].Stock[0], 1000) && near(s.Worlds[1].Stock[1], 1000), "intermediate depot stays untouched by travelling fleet");
            check(near(s.PhysicalTotal(0), fuel) && near(s.PhysicalTotal(1), ammo - s.Consumed[1]), "blocked and intermediate fleet consumption conserves physical resources");

            s = Isolated(); s.Routes[0].Open = s.Routes[1].Open = false;
            check(s.TransferFleet(0, true), "hyperjump starts with closed supply routes");
            s.Advance(.1);
            check(s.Fleet.Travelling && near(s.Fleet.Fuel, 30) && near(s.Fleet.Ammunition, 40), "hyperjump cannot refill from the departure depot in transit");
            s.Advance(.16);
            check(!s.Fleet.Travelling && s.Fleet.Location == 0 && near(s.Fleet.Fuel, 30), "arrival itself does not teleport depot stores aboard");
            s.Advance(.02);
            check(near(s.Fleet.Fuel, 30.12) && near(s.Fleet.Ammunition, 40.16), "refill resumes on the next tick in destination orbit");

            var x = new EconomySimulation(); var y = new EconomySimulation();
            x.Fleet.Exercise = y.Fleet.Exercise = true;
            x.Advance(2); for (int i = 0; i < 200; i++) y.Advance(.01);
            check(near(x.Day, y.Day) && near(x.PhysicalTotal(0), y.PhysicalTotal(0)) && near(x.Lines[0].Work, y.Lines[0].Work), "fixed tick is independent of frame chunking");
            double outputBefore = x.Available[0]; x.Advance(.001);
            check(near(x.Available[0], outputBefore), "strategic resources are not accumulated with elapsed time");
            bool rejected = false; try { x.Advance(double.NaN); } catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "reject nonfinite elapsed time");
            s = Isolated(); line = s.AddLine(ProjectKind.Storage, 1, 120, 2); s.Advance(1);
            check(s.Worlds[2].StorageCapacity[0] == 500 && s.Worlds[2].StorageCapacity[1] == 500, "finished storage project adds physical capacity");
            s = Isolated(); line = s.AddLine(ProjectKind.Route, 1, 160, 0); s.Advance(1);
            check(s.Routes[0].Capacity == 100, "finished route project increases existing route capacity");
            return checks + " economy v0.3 checks passed";
        }
    }
}
