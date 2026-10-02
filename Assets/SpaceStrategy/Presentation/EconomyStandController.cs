using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpaceStrategy.Domain.Economy;
using UnityEngine;

namespace SpaceStrategy.Presentation
{
    public sealed class EconomyStandController : MonoBehaviour
    {
        private EconomySimulation simulation;
        private Font font;
        private readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        private Vector2 queueScroll;
        private int tab, world = 1, destination = 1, projectTarget = 1, speed = 1;
        private bool paused;
        private int runtimeErrors;
        private int paintedFrames;
        private static readonly Color Background = new Color(.025f, .041f, .062f);
        private static readonly Color Panel = new Color(.047f, .073f, .102f);
        private static readonly Color Card = new Color(.065f, .101f, .137f);
        private static readonly Color TextColor = new Color(.87f, .93f, .96f);
        private static readonly Color Muted = new Color(.49f, .62f, .71f);
        private static readonly Color Teal = new Color(.28f, .84f, .72f);
        private static readonly Color Amber = new Color(1, .69f, .36f);

        private void Awake()
        {
            Initialize();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--economy-smoke") >= 0) StartCoroutine(SmokeTest());
        }
        private void OnEnable() { if (simulation == null || font == null) Initialize(); }
        private void Initialize()
        {
            simulation = new EconomySimulation();
            Application.runInBackground = true; Application.targetFrameRate = 60;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 16);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            styles.Clear();
            Application.logMessageReceived -= TrackError; Application.logMessageReceived += TrackError;
        }
        private void TrackError(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++; }
        private void OnDestroy() { Application.logMessageReceived -= TrackError; if (font != null) Destroy(font); }
        private void Update()
        { AdvanceStand(); }
        public void AdvanceStand()
        {
            if (!paused) simulation.Advance(Math.Min(Time.unscaledDeltaTime, .2) * .1 * speed);
        }

        private void OnGUI()
        { DrawStand(); }
        public void DrawStand()
        {
            if (simulation == null) return;
            if (Event.current.type == EventType.Repaint) paintedFrames++;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            { paused = !paused; Event.current.Use(); }
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 1000f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 1000 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, 1600, 1000), Background);
            Label(30, 22, 720, "SPACE STRATEGY  /  ПРОИЗВОДСТВО", 26, TextColor, true);
            Label(32, 62, 750, "СТЕНД 03  ·  РЕСУРСЫ / ПРОИЗВОДСТВЕННЫЕ ЛИНИИ / ЛОГИСТИКА", 12, Muted);
            Label(840, 26, 150, "День " + simulation.Day.ToString("0.00"), 18, TextColor);
            if (Button(1000, 23, 110, paused ? "Продолжить" : "Пауза", paused)) paused = !paused;
            if (Button(1120, 23, 86, "+0.25 сут")) { paused = true; simulation.Advance(.25); }
            int[] speeds = { 1, 4, 16 };
            for (int i = 0; i < 3; i++) if (Button(1218 + i * 64, 23, 56, speeds[i] + "×", speed == speeds[i])) speed = speeds[i];
            if (Button(1420, 23, 148, "Сбросить стенд")) { simulation = new EconomySimulation(); paused = true; queueScroll = Vector2.zero; }
            Label(1000, 62, 560, "Пробел — пауза  ·  1 сутки = 10 секунд при 1×", 12, Muted);
            DrawBudget();
            DrawQueue();
            DrawDetails();
            Label(32, 946, 1530, simulation.LastEvent, 13, TextColor);
            Label(32, 976, 1530, "ТЕСТОВЫЕ ПАРАМЕТРЫ, НЕ ФИНАЛЬНЫЙ БАЛАНС  ·  Нет миссий  ·  Построено: " + simulation.FrigatesBuilt + " фрегатов / " + simulation.CruisersBuilt + " крейсеров", 11, Muted);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawBudget()
        {
            Fill(new Rect(30, 100, 1540, 88), Panel);
            string[] names = { "ВСЕ ПЧ / СУТ", "АВТОНОМНЫЕ НУЖДЫ", "ГРАЖДАНСКАЯ ДОЛЯ", "ПУЛ ИГРОКА", "СВОБОДНЫЕ ПЧ", "ТОПЛИВО / БОЕПРИПАСЫ" };
            string[] values = { simulation.TotalPh.ToString("0"), simulation.AutonomousPh.ToString("0"), simulation.CivilianPh.ToString("0") + " / " + (simulation.Settings.TnpFraction * 100).ToString("0") + "%", simulation.PlayerPh.ToString("0"), simulation.FreePh.ToString("0.0"), simulation.StorageFill(0).ToString("P0") + " / " + simulation.StorageFill(1).ToString("P0") };
            for (int i = 0; i < 6; i++)
            { Label(48 + i * 253, 110, 246, names[i], 11, Muted); Label(48 + i * 253, 140, 246, values[i], 24, i == 4 ? Teal : TextColor, true); }
        }

        private void DrawQueue()
        {
            Fill(new Rect(30, 205, 960, 721), Panel);
            Label(48, 218, 550, "ИМПЕРСКАЯ ПРОИЗВОДСТВЕННАЯ ОЧЕРЕДЬ", 17, TextColor, true);
            Label(48, 250, 890, "ПЧ назначаются вручную. Порядок строк не меняет выделения и эффективность.", 12, Muted);
            for (int i = 0; i < 4; i++)
                if (Button(48 + i * 228, 284, 217, "+ " + EconomySimulation.Recipes[i].Name))
                    simulation.AddLine((ProjectKind)i, i < 2 ? 2 : 1, Math.Min(20, simulation.FreePh), projectTarget);
            Label(48, 326, 310, "Цель новой стройки / проекта:", 12, Muted);
            for (int i = 0; i < 3; i++) if (Button(366 + i * 188, 319, 177, simulation.Worlds[i].Name, projectTarget == i)) projectTarget = i;
            Label(48, 359, 900, "Склады: выбранный мир. Маршрут: Эридан → участок 1; Земля / Бастион → участок 2.", 11, Muted);
            Rect area = new Rect(42, 394, 938, 522);
            queueScroll = GUI.BeginScrollView(area, queueScroll, new Rect(0, 0, 910, Math.Max(514, simulation.Lines.Count * 193)));
            ProductionLine delete = null, move = null; int moveOffset = 0;
            for (int index = 0; index < simulation.Lines.Count; index++)
            {
                var line = simulation.Lines[index]; var recipe = EconomySimulation.Recipes[(int)line.Kind]; float y = index * 193;
                Fill(new Rect(0, y, 908, 182), Card);
                Label(14, y + 10, 570, "#" + line.Id + "  " + recipe.Name + "  ·  " + line.Completed + " / " + line.Quantity, 18, TextColor, true);
                if (Button(594, y + 8, 42, "↑")) { move = line; moveOffset = -1; }
                if (Button(642, y + 8, 42, "↓")) { move = line; moveOffset = 1; }
                if (Button(690, y + 8, 90, line.Paused ? "Пуск" : "Пауза", line.Paused)) { line.Paused = !line.Paused; simulation.Recalculate(); }
                if (Button(786, y + 8, 107, "Удалить")) delete = line;
                Label(14, y + 48, 530, "ПЧ " + line.AssignedPh.ToString("0") + "   /   Эфф. " + line.Efficiency.ToString("P0") + "   /   Ресурсы " + line.ResourceFactor.ToString("P0"), 14, Muted);
                Label(560, y + 48, 332, "Темп " + line.Rate.ToString("0.0") + " ПЧ/сут", 18, Teal, true);
                // Shared scale depends on the empire pool, never on other lines' assignments.
                float maximum = (float)Math.Max(1, simulation.PlayerPh);
                double ceiling = line.AssignedPh + simulation.FreePh;
                bool previousChanged = GUI.changed;
                GUI.changed = false;
                float ph = GUI.HorizontalSlider(new Rect(16, y + 88, 355, 22), (float)line.AssignedPh, 0, maximum);
                bool sliderChanged = GUI.changed;
                GUI.changed |= previousChanged;
                // Only a real gesture may change allocation; a pool change/repaint must not.
                if (sliderChanged)
                {
                    double requested = Math.Min(Math.Round(ph), Math.Floor(ceiling + 1e-8));
                    if (Math.Abs(requested - line.AssignedPh) > .5) simulation.SetAllocation(line, requested);
                }
                Label(16, y + 107, 355, "Шкала 0–" + maximum.ToString("0") + " ПЧ  ·  Лимит сейчас " + ceiling.ToString("0"), 10, Muted);
                if (Button(390, y + 78, 62, "−10")) simulation.SetAllocation(line, Math.Max(0, line.AssignedPh - 10));
                if (Button(458, y + 78, 62, "+10")) simulation.SetAllocation(line, Math.Min(line.AssignedPh + 10, line.AssignedPh + simulation.FreePh));
                if (Button(526, y + 78, 85, "+1 шт")) simulation.ExtendLine(line);
                if (Button(617, y + 78, 130, "Сменить тип")) simulation.ChangeProduct(line, (ProjectKind)(((int)line.Kind + 1) % 4));
                Label(760, y + 81, 136, line.Active ? "В РАБОТЕ" : line.Completed >= line.Quantity ? "ГОТОВО" : "ПАУЗА", 12, line.Active ? Teal : Amber);
                double progress = line.Completed >= line.Quantity ? 1 : line.Work / recipe.Work;
                Bar(new Rect(14, y + 124, 880, 7), progress, Teal);
                string eta = line.Rate > 0 ? ((recipe.Work - line.Work) / line.Rate).ToString("0.0") + " сут до следующего" : "Нет выпуска";
                Label(14, y + 140, 350, "Прогресс " + progress.ToString("P0") + "  ·  " + eta, 12, TextColor);
                string resources = "";
                for (int r = 0; r < 6; r++) if (line.Needed[r] > 0)
                    resources += (resources.Length > 0 ? "  ·  " : "") + EconomySimulation.ResourceNames[r] + " " + line.Granted[r].ToString("0.0") + "/" + line.Needed[r].ToString("0.0");
                Label(370, y + 140, 524, resources, 11, Muted);
            }
            GUI.EndScrollView();
            if (move != null) simulation.MoveLine(move, moveOffset);
            if (delete != null) { simulation.Lines.Remove(delete); simulation.Recalculate(); }
        }

        private void DrawDetails()
        {
            Fill(new Rect(1008, 205, 562, 721), Panel);
            string[] tabs = { "РЕСУРСЫ", "СКЛАДЫ", "СЕТЬ", "НАСТРОЙКИ" };
            for (int i = 0; i < 4; i++) if (Button(1023 + i * 134, 220, 125, tabs[i], tab == i, 11)) tab = i;
            if (tab == 0) DrawResources();
            if (tab == 1) DrawStores();
            if (tab == 2) DrawNetwork();
            if (tab == 3) DrawSettings();
        }
        private void DrawResources()
        {
            Label(1028, 272, 515, "СТРАТЕГИЧЕСКАЯ МОЩНОСТЬ, НЕ СКЛАД", 14, TextColor, true);
            Label(1028, 306, 260, "Ресурс", 12, Muted);
            string[] columns = { "Потенц.", "Доступ.", "Занято", "Свобод." };
            for (int i = 0; i < 4; i++) Label(1255 + i * 71, 306, 69, columns[i], 10, Muted);
            for (int r = 0; r < 6; r++)
            {
                float y = 338 + r * 47;
                Label(1028, y, 228, EconomySimulation.ResourceNames[r], 14, TextColor);
                double[] values = { simulation.Potential[r], simulation.Available[r], simulation.Used[r], Math.Max(0, simulation.Available[r] - simulation.Used[r]) };
                for (int i = 0; i < 4; i++) Label(1255 + i * 71, y, 69, values[i].ToString("0.0"), 14, i == 1 && values[1] < values[0] - .01 ? Amber : TextColor);
            }
            Label(1028, 636, 515, "РЕСУРСНЫЙ ПОТЕНЦИАЛ ПЛАНЕТЫ", 13, TextColor, true);
            WorldSelector(673);
            var planet = simulation.Worlds[world];
            if (Button(1028, 716, 515, planet.ExtractionEnabled ? "Источники включены — отключить" : "Источники отключены — включить", planet.ExtractionEnabled)) { planet.ExtractionEnabled = !planet.ExtractionEnabled; simulation.Recalculate(); }
            for (int r = 0; r < 6; r++)
            {
                float x = 1028 + (r % 2) * 263, y = 759 + (r / 2) * 52;
                Label(x, y, 172, EconomySimulation.ResourceNames[r] + " " + planet.Potential[r].ToString("0.0"), 11, Muted);
                if (Button(x + 174, y - 2, 35, "−", false, 12)) { planet.Potential[r] = Math.Max(0, planet.Potential[r] - .5); simulation.Recalculate(); }
                if (Button(x + 214, y - 2, 35, "+", false, 12)) { planet.Potential[r] += .5; simulation.Recalculate(); }
            }
        }
        private void DrawStores()
        {
            Label(1028, 272, 515, "ЛОКАЛЬНЫЕ ЗАПАСЫ И ГРУЗЫ В ПУТИ", 14, TextColor, true);
            WorldSelector(310);
            var planet = simulation.Worlds[world];
            for (int r = 0; r < 2; r++)
            {
                float y = 364 + r * 184;
                Label(1028, y, 510, (r == 0 ? "ТОПЛИВО" : "БОЕПРИПАСЫ") + "  " + planet.Stock[r].ToString("0") + " / " + planet.StorageCapacity[r].ToString("0"), 19, TextColor, true);
                Bar(new Rect(1028, y + 40, 515, 10), planet.Stock[r] / planet.StorageCapacity[r], r == 0 ? Teal : Amber);
                Label(1028, y + 64, 510, "В пути: " + simulation.InTransit(world, r).ToString("0.0") + "  ·  Защищено резервом: " + planet.Reserve[r].ToString("0"), 13, Muted);
                Label(1028, y + 95, 510, "Выпуск: " + planet.ProducedPerDay[r].ToString("0.0") + "/сут   ·   Расход базы: " + planet.UsagePerDay[r].ToString("0.0") + "/сут", 13, TextColor);
                if (Button(1028, y + 129, 250, "Предприятие −10/сут")) { planet.ProductionPerDay[r] = Math.Max(0, planet.ProductionPerDay[r] - 10); simulation.Recalculate(); }
                if (Button(1288, y + 129, 255, "Предприятие +10/сут")) { planet.ProductionPerDay[r] += 10; simulation.Recalculate(); }
            }
            Label(1028, 757, 510, "Физических партий в пути: " + simulation.Cargoes.Count, 16, TextColor);
            Wrap(1028, 797, 515, 100, "Запас не телепортируется. Локальные предприятия наполняют местный склад; излишки перевозятся. Закрытие участка удерживает груз в пути. Общий процент сверху — сводка складов, не новый глобальный склад.", 13, Muted);
        }
        private void DrawNetwork()
        {
            Label(1028, 272, 515, "ОБЩАЯ ДВУСТОРОННЯЯ МОЩНОСТЬ", 14, TextColor, true);
            for (int e = 0; e < 2; e++)
            {
                var route = simulation.Routes[e]; float y = 310 + e * 170;
                Label(1028, y, 510, route.Name + "   " + route.Load.ToString("0.0") + " / " + route.EffectiveCapacity.ToString("0"), 18, TextColor, true);
                Bar(new Rect(1028, y + 38, 515, 8), route.EffectiveCapacity > 0 ? route.Load / route.EffectiveCapacity : 0, route.Demand > route.EffectiveCapacity ? Amber : Teal);
                Label(1028, y + 60, 510, "Спрос " + route.Demand.ToString("0.0") + "   ·   Стратег. " + route.CategoryLoad[0].ToString("0.0") + " / Т " + route.CategoryLoad[1].ToString("0.0") + " / Б " + route.CategoryLoad[2].ToString("0.0") + " / Ф " + route.CategoryLoad[3].ToString("0.0"), 12, Muted);
                if (Button(1028, y + 94, 120, route.Open ? "Закрыть" : "Открыть", !route.Open)) { route.Open = !route.Open; simulation.Recalculate(); }
                if (Button(1158, y + 94, 100, "Рейд", route.Raided)) { route.Raided = !route.Raided; simulation.Recalculate(); }
                if (Button(1268, y + 94, 130, "Мощность −20")) { route.Capacity = Math.Max(0, route.Capacity - 20); simulation.Recalculate(); }
                if (Button(1408, y + 94, 135, "Мощность +20")) { route.Capacity += 20; simulation.Recalculate(); }
            }
            var fleet = simulation.Fleet;
            Label(1028, 663, 510, "ФЛОТ  ·  " + simulation.Worlds[fleet.Location].Name + (fleet.Travelling ? " → " + simulation.Worlds[fleet.Destination].Name : ""), 17, TextColor, true);
            Label(1028, 700, 515, "Топливо " + fleet.Fuel.ToString("0.0") + "/100  ·  Боеприпасы " + fleet.Ammunition.ToString("0.0") + "/80", 14, Muted);
            for (int i = 0; i < 3; i++) if (Button(1028 + i * 174, 739, 164, simulation.Worlds[i].Name, destination == i)) destination = i;
            if (Button(1028, 784, 250, "По сети к цели")) simulation.TransferFleet(destination, false);
            if (Button(1288, 784, 255, "Гиперпрыжок")) simulation.TransferFleet(destination, true);
            if (Button(1028, 831, 250, fleet.Exercise ? "Учения: остановить" : "Учения: расход БК", fleet.Exercise)) fleet.Exercise = !fleet.Exercise;
            Label(1288, 839, 255, fleet.Travelling ? "Участок " + (fleet.Leg + 1) + "  ·  " + fleet.Progress.ToString("P0") : "Пополнение из местного склада", 11, Muted);
            Label(1028, 885, 515, "Фиксированная сеть; все потоки равноприоритетны.", 12, Muted);
        }
        private void DrawSettings()
        {
            Label(1028, 272, 515, "ЭКСПЕРИМЕНТАЛЬНЫЕ ПАРАМЕТРЫ", 14, TextColor, true);
            var settings = simulation.Settings;
            settings.TnpFraction = Slider(320, "ТНП / общие ПЧ", settings.TnpFraction, 0, .7f, "P0");
            settings.InitialEfficiency = Slider(400, "Стартовая эффективность новых ПЧ", settings.InitialEfficiency, .05f, 1, "P0");
            settings.EfficiencyGainPerDay = Slider(480, "Базовый разгон / сутки при 100% ресурсов", settings.EfficiencyGainPerDay, .01f, .25f, "P1");
            settings.CargoDaysPerLeg = Slider(560, "Время груза / участок, сутки", settings.CargoDaysPerLeg, .1f, 2, "0.0");
            Wrap(1028, 654, 515, 54, "Разгон = базовый × ресурсный коэффициент. При 0% ресурсов эффективность сохраняется, но не растёт.", 13, Teal);
            Wrap(1028, 714, 515, 180, "Здесь не утверждённые правила баланса, а настройки лаборатории. ТНП считается от общих ПЧ. При нехватке общего пула назначения пропорционально ограничиваются. Частичное снятие мощности пропорционально всем её порциям. Продление серии сохраняет линию. Смена продукции сбрасывает незавершённую работу.", 13, Muted);
            simulation.Recalculate();
        }
        private float Slider(float y, string caption, double value, float min, float max, string format)
        {
            Label(1028, y, 515, caption + "  " + value.ToString(format), 14, TextColor);
            return GUI.HorizontalSlider(new Rect(1028, y + 38, 515, 25), (float)value, min, max);
        }
        private void WorldSelector(float y)
        { for (int i = 0; i < 3; i++) if (Button(1028 + i * 174, y, 164, simulation.Worlds[i].Name, world == i)) world = i; }
        private GUIStyle Style(int size, Color color, bool bold = false, bool wrap = false)
        {
            string key = size + ":" + color.ToString() + ":" + bold + ":" + wrap;
            if (!styles.TryGetValue(key, out var style))
            {
                style = new GUIStyle { font = font, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = wrap, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                style.normal.textColor = color; styles[key] = style;
            }
            return style;
        }
        private void Label(float x, float y, float width, string text, int size, Color color, bool bold = false)
        { GUI.Label(new Rect(x, y, width, 28), text, Style(size, color, bold)); }
        private void Wrap(float x, float y, float width, float height, string text, int size, Color color)
        { GUI.Label(new Rect(x, y, width, height), text, Style(size, color, false, true)); }
        private bool Button(float x, float y, float width, string text, bool active = false, int size = 12)
        {
            Rect rect = new Rect(x, y, width, 33);
            Fill(rect, active ? new Color(.11f, .29f, .29f) : new Color(.10f, .15f, .20f));
            var style = new GUIStyle(Style(size, active ? Teal : TextColor)); style.alignment = TextAnchor.MiddleCenter;
            return GUI.Button(rect, text, style);
        }
        private static void Fill(Rect rect, Color color)
        { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private static void Bar(Rect rect, double value, Color color)
        { Fill(rect, new Color(.10f, .16f, .20f)); rect.width *= (float)Math.Max(0, Math.Min(1, value)); Fill(rect, color); }

        private IEnumerator SmokeTest()
        {
            paused = true;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--economy-smoke-output");
            string directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(directory);
            // The reused Windows player host may still be showing its native splash screen.
            yield return new WaitForSecondsRealtime(3);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "economy-stand.png"));
            yield return new WaitForSecondsRealtime(.5f);
            bool passed = false;
            try { Debug.Log(EconomyChecks.Run()); passed = true; }
            catch (Exception exception) { Debug.LogException(exception); }
            tab = 2;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "economy-network.png"));
            yield return new WaitForSecondsRealtime(.5f);
            passed = passed && runtimeErrors == 0 && paintedFrames > 1;
            File.WriteAllText(Path.Combine(directory, "smoke-result.json"), "{\"passed\":" + (passed ? "true" : "false") + ",\"runtimeErrors\":" + runtimeErrors + ",\"paintedFrames\":" + paintedFrames + "}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
