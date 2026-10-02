using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpaceStrategy.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceStrategy.Presentation
{
    public sealed class PrototypeController : MonoBehaviour
    {
        private StrategySimulation simulation;
        private int selectedPlanet = 1;
        private int selectedRoute = 1;
        private int selectedDestination = 1;
        private int speed = 1;
        private bool paused;
        private Font font;
        private Texture2D stars;
        private Texture2D circle;
        private Texture2D ship;
        private readonly Texture2D[] planets = new Texture2D[3];
        private readonly Dictionary<int, GUIStyle> textStyles = new Dictionary<int, GUIStyle>();
        private int runtimeErrors;
        private int consumedClickFrame = -1;
        private int activeSlider = -1;
        private static readonly Color Background = new Color(0.025f, 0.043f, 0.066f);
        private static readonly Color Panel = new Color(0.052f, 0.078f, 0.11f);
        private static readonly Color Border = new Color(0.13f, 0.19f, 0.25f);
        private static readonly Color Ink = new Color(0.88f, 0.94f, 0.96f);
        private static readonly Color Muted = new Color(0.48f, 0.61f, 0.70f);
        private static readonly Color Teal = new Color(0.31f, 0.83f, 0.72f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.35f);
        private static readonly Color Red = new Color(0.97f, 0.38f, 0.37f);
        private static readonly Vector2[] Positions = { new Vector2(190, 360), new Vector2(535, 280), new Vector2(875, 460) };

        private void Awake()
        {
            Initialize();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--prototype-smoke") >= 0)
                StartCoroutine(SmokeTest());
        }

        private void OnEnable()
        {
            // A script reload in Play mode discards this non-serialized test state.
            // Reinitialize the stand instead of leaving OnGUI with null references.
            if (simulation == null || font == null || stars == null) Initialize();
        }

        private void Initialize()
        {
            simulation = new StrategySimulation();
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 16);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textStyles.Clear();
            GenerateTextures();
            Application.logMessageReceived -= TrackErrors;
            Application.logMessageReceived += TrackErrors;
        }

        private void TrackErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= TrackErrors;
            foreach (Texture2D texture in planets) if (texture != null) Destroy(texture);
            if (stars != null) Destroy(stars);
            if (circle != null) Destroy(circle);
            if (ship != null) Destroy(ship);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
            if (!paused) simulation.Advance(Math.Min(Time.unscaledDeltaTime, 0.5) * 0.25 * speed);
        }

        private void OnGUI()
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, 1600, 900), Background);
            DrawHeader();
            DrawMap();
            DrawFleet();
            DrawRoutes();
            DrawPlanet();
            DrawProduction();
            Text(new Rect(36, 868, 1528, 24), "СОБЫТИЕ   " + simulation.LastEvent, 13, Muted);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawHeader()
        {
            Text(new Rect(36, 24, 700, 38), "SPACE STRATEGY", 29, Ink, true);
            Text(new Rect(37, 68, 670, 24), "ТЕСТОВЫЙ СТЕНД 02  /  ЭКОНОМИКА И ЛОГИСТИКА", 12, Muted);
            int day = (int)(simulation.Hour / 24) + 1;
            Text(new Rect(760, 32, 290, 30), "День " + day + "  ·  " + ((int)simulation.Hour % 24).ToString("00") + ":00", 19, Ink);
            if (Button(new Rect(1060, 30, 110, 38), paused ? "Продолжить" : "Пауза", paused)) paused = !paused;
            int[] speeds = { 1, 4, 8 };
            for (int i = 0; i < speeds.Length; i++)
            {
                int value = speeds[i];
                if (Button(new Rect(1182 + i * 68, 30, 58, 38), value + "×", speed == value)) speed = value;
            }
            if (Button(new Rect(1400, 30, 164, 38), "Начать заново")) { simulation = new StrategySimulation(); paused = false; }
            Text(new Rect(1060, 78, 504, 20), "Пробел — пауза  ·  1 игровой час = 4 секунды при 1×", 12, Muted);
            Fill(new Rect(36, 105, 1528, 1), Border);
        }

        private void DrawMap()
        {
            Box(new Rect(36, 120, 1000, 530));
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(37, 121, 998, 528), stars, ScaleMode.StretchToFill);
            for (int x = 90; x < 1036; x += 100) Fill(new Rect(x, 165, 1, 458), new Color(0.07f, 0.11f, 0.15f, 0.35f));
            for (int y = 190; y < 650; y += 100) Fill(new Rect(60, y, 952, 1), new Color(0.07f, 0.11f, 0.15f, 0.35f));
            Text(new Rect(60, 141, 330, 23), "СЕКТОР ОРИОН", 13, Ink, true);
            Text(new Rect(712, 142, 300, 22), "3 мира  /  2 маршрута  /  1 флот", 12, Muted);
            for (int i = 0; i < 2; i++)
            {
                RouteState route = simulation.Routes[i];
                Vector2 a = Positions[i], b = Positions[i + 1];
                Color color = !route.Open ? Red : route.Overloaded ? Amber : Teal;
                Line(a, b, selectedRoute == i ? 5 : 3, new Color(color.r, color.g, color.b, 0.38f));
                if (route.Open)
                {
                    for (int j = 0; j < 12; j++)
                    {
                        float t = (j / 12f + (float)simulation.Hour * 0.04f) % 1;
                        Vector2 dot = Vector2.Lerp(a, b, t);
                        Fill(new Rect(dot.x - 2, dot.y - 2, 4, 4), color);
                    }
                }
                Vector2 middle = Vector2.Lerp(a, b, 0.5f);
                Rect badge = new Rect(middle.x - 100, middle.y - 45, 200, 28);
                if (Button(badge, route.Demand.ToString("0") + " / " + route.EffectiveCapacity.ToString("0") + "  ·  " + (route.Open ? route.Overloaded ? "ПЕРЕГРУЗКА" : "НОРМА" : "ЗАКРЫТ"), selectedRoute == i, true, 11)) selectedRoute = i;
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = Positions[i];
                Rect hit = new Rect(p.x - 62, p.y - 62, 124, 158);
                if (Pressed(hit)) { selectedPlanet = i; selectedDestination = i; }
                GUI.color = selectedPlanet == i ? Teal : Border;
                GUI.DrawTexture(new Rect(p.x - 55, p.y - 55, 110, 110), circle);
                GUI.color = Background;
                GUI.DrawTexture(new Rect(p.x - 52, p.y - 52, 104, 104), circle);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(p.x - 43, p.y - 43, 86, 86), planets[i]);
                Text(new Rect(p.x - 110, p.y + 66, 220, 26), simulation.Planets[i].Name, 18, Ink, true, TextAnchor.MiddleCenter);
                string detail = i == 0 ? simulation.MiningOutput.ToString("0") + " сырья / ч" : simulation.Planets[i].Output.ToString("0.0") + " ПЧ / ч";
                Text(new Rect(p.x - 110, p.y + 96, 220, 22), detail, 13, i == 0 && !simulation.MiningEnabled ? Red : Muted, false, TextAnchor.MiddleCenter);
            }
            FleetState f = simulation.Fleet;
            Vector2 fleetPosition = f.Travelling ? Vector2.Lerp(Positions[f.LegOrigin], Positions[f.LegDestination],
                (float)(f.Mode == TransferMode.Route ? f.LegProgress : f.TravelProgress)) : Positions[f.Location] + new Vector2(-75, -74);
            GUI.color = f.SupplyRatio < 0.9 ? Amber : Teal;
            GUI.DrawTexture(new Rect(fleetPosition.x - 15, fleetPosition.y - 15, 30, 30), ship);
            Text(new Rect(fleetPosition.x + 22, fleetPosition.y - 12, 130, 24), "1-й флот", 13, Teal, true);
            Text(new Rect(60, 610, 900, 22), "Планета выбирает цель флота; команда — справа. Линии показывают состояние общих маршрутов.", 12, Muted);
        }

        private void DrawFleet()
        {
            FleetState f = simulation.Fleet;
            Box(new Rect(1060, 120, 504, 260));
            Text(new Rect(1082, 139, 300, 26), "1-Й УДАРНЫЙ ФЛОТ", 17, Ink, true);
            string where = f.Travelling ? "В пути → " + simulation.Planets[f.Destination].Name : "Орбита: " + simulation.Planets[f.Location].Name;
            Text(new Rect(1082, 172, 400, 22), f.Ships + " фрегата  ·  " + where, 13, Muted);
            Text(new Rect(1082, 204, 160, 24), "Снабжение", 14, Ink);
            Text(new Rect(1430, 202, 112, 26), Percent(f.SupplyRatio), 20, f.SupplyRatio < 0.9 ? Amber : Teal, true, TextAnchor.MiddleRight);
            Bar(new Rect(1082, 233, 460, 6), f.SupplyRatio, f.SupplyRatio < 0.9 ? Amber : Teal);
            Text(new Rect(1082, 249, 460, 22), "Поставки " + f.NetworkSupply.ToString("0.0") + " / расход " + f.Demand.ToString("0") + " в час  ·  запас " + f.SupplyStock.ToString("0") + " / 150", 12, Muted);
            Text(new Rect(1082, 273, 460, 20), "Организация " + f.Organization.ToString("0") + "%     Топливо " + f.Fuel.ToString("0") + "%", 13, Ink);
            if (f.Travelling) Bar(new Rect(1082, 294, 460, 3), f.TravelProgress, Teal);
            for (int i = 0; i < simulation.Planets.Length; i++)
                if (Button(new Rect(1082 + i * 156, 300, 148, 24), simulation.Planets[i].Name,
                    selectedDestination == i, !f.Travelling, 11)) selectedDestination = i;
            string routeReason = simulation.TransferBlockReason(selectedDestination, TransferMode.Route);
            string jumpReason = simulation.TransferBlockReason(selectedDestination, TransferMode.Hyperjump);
            string routeLabel = "По сети · " + simulation.MinimumTravelHours(selectedDestination, TransferMode.Route) + "+ ч / −"
                + simulation.TransferFuelCost(selectedDestination, TransferMode.Route) + " топлива";
            if (Button(new Rect(1082, 332, 222, 32), routeLabel, false, routeReason == null, 11)) simulation.Transfer(selectedDestination, TransferMode.Route);
            if (Button(new Rect(1316, 332, 226, 32), "Гиперпрыжок · 2 ч / −30", false, jumpReason == null, 11)) simulation.Transfer(selectedDestination, TransferMode.Hyperjump);
            if (!f.Travelling && routeReason != null) Text(new Rect(1082, 365, 460, 14), routeReason, 10, Muted);
        }

        private void DrawRoutes()
        {
            Box(new Rect(1060, 396, 504, 254));
            Text(new Rect(1082, 411, 350, 24), "УПРАВЛЕНИЕ МАРШРУТОМ", 13, Ink, true);
            if (Button(new Rect(1082, 446, 220, 30), "Эридан IV ↔ Земля", selectedRoute == 0, true, 12)) selectedRoute = 0;
            if (Button(new Rect(1314, 446, 228, 30), "Земля ↔ Бастион", selectedRoute == 1, true, 12)) selectedRoute = 1;
            RouteState r = simulation.Routes[selectedRoute];
            Text(new Rect(1082, 489, 460, 24), "Спрос " + r.Demand.ToString("0.0") + "   /   доступно " + r.EffectiveCapacity.ToString("0") + "   ·   доставляется " + Percent(r.DeliveryRatio), 13, r.Overloaded ? Amber : Teal);
            Text(new Rect(1082, 521, 150, 22), "Мощность: " + r.Capacity.ToString("0"), 12, Muted);
            GUI.backgroundColor = Teal;
            float capacity = Slider(new Rect(1238, 527, 304, 18), (float)r.Capacity, 0, 180, 10 + selectedRoute);
            GUI.backgroundColor = Color.white;
            if (Math.Abs(capacity - r.Capacity) > 0.5) { r.Capacity = Math.Round(capacity); simulation.Recalculate(); }
            if (Button(new Rect(1082, 556, 146, 30), r.Open ? "Закрыть" : "Открыть", !r.Open, true, 12)) { r.Open = !r.Open; simulation.Recalculate(); }
            if (Button(new Rect(1240, 556, 146, 30), r.Raided ? "Снять рейд" : "Рейд −50%", r.Raided, true, 12)) { r.Raided = !r.Raided; simulation.Recalculate(); }
            if (Button(new Rect(1398, 556, 144, 30), "Мощность 20", false, true, 12)) { r.Capacity = 20; simulation.Recalculate(); }
            if (Button(new Rect(1082, 600, 146, 30), "Восстановить", false, true, 12)) { r.Capacity = selectedRoute == 0 ? 100 : 70; r.Raided = false; r.Open = true; simulation.Recalculate(); }
            Text(new Rect(1240, 595, 302, 42), "Экономика, снабжение и переброска делят одну мощность.", 12, Muted);
        }

        private void DrawPlanet()
        {
            Box(new Rect(36, 674, 1000, 178));
            PlanetState p = simulation.Planets[selectedPlanet];
            Text(new Rect(58, 692, 325, 29), p.Name + " / " + p.Role, 16, Ink, true);
            Text(new Rect(58, 729, 518, 44), simulation.PlanetCause(selectedPlanet), 13, p.Availability < 0.9 && selectedPlanet != 0 ? Amber : Muted);
            if (selectedPlanet == 0)
            {
                Text(new Rect(606, 695, 380, 24), "Потенциал добычи: 92 единицы / ч", 14, Ink);
                if (Button(new Rect(606, 737, 400, 37), simulation.MiningEnabled ? "Приостановить добычу" : "Возобновить добычу", !simulation.MiningEnabled)) { simulation.MiningEnabled = !simulation.MiningEnabled; simulation.Recalculate(); }
                Text(new Rect(58, 811, 850, 22), "Потеря поставщика сначала расходует запасы промышленных миров, затем снижает их выпуск.", 12, Muted);
            }
            else
            {
                Text(new Rect(58, 788, 510, 23), "Выпуск " + p.Output.ToString("0.0") + " ПЧ/ч  ·  сырьё " + p.RawStock.ToString("0") + " / " + p.MaxRawStock.ToString("0"), 13, Ink);
                Text(new Rect(58, 817, 510, 21), "Мощность " + p.Capacity.ToString("0") + " × зрелость " + Percent(p.Efficiency) + " × темп " + Percent(p.Tempo) + " × сырьё " + Percent(p.Availability), 12, Muted);
                Text(new Rect(606, 696, 380, 22), "Темп производства: " + Percent(p.Tempo), 13, Ink);
                float tempo = Slider(new Rect(606, 737, 400, 20), (float)p.Tempo, 0.25f, 1.2f, 20 + selectedPlanet);
                if (Math.Abs(tempo - p.Tempo) > 0.005) { p.Tempo = Math.Round(tempo, 2); simulation.Recalculate(); }
                if (selectedPlanet == 1 && Button(new Rect(606, 776, 400, 36), simulation.IndustrialPriority ? "Приоритет: промышленный" : "Приоритет: сбалансированный", simulation.IndustrialPriority)) { simulation.IndustrialPriority = !simulation.IndustrialPriority; simulation.Recalculate(); }
                if (selectedPlanet == 2) Text(new Rect(606, 783, 400, 48), "База зависит от двух маршрутов. Локальный запас смягчает перебои с сырьём.", 13, Muted);
            }
        }

        private void DrawProduction()
        {
            Box(new Rect(1060, 674, 504, 178));
            Text(new Rect(1082, 692, 460, 24), "ВЕРФЬ ЗЕМЛИ", 13, Ink, true);
            double available = Math.Max(0, simulation.Planets[1].Output - simulation.Fleet.Demand * 0.7);
            Text(new Rect(1082, 725, 460, 22), simulation.ShipQueued ? "Фрегат: " + simulation.ShipWork.ToString("0") + " / 2000 ПЧ  ·  " + available.ToString("0.0") + " ПЧ/ч" : "Фрегат · 2000 ПЧ  /  +6 снабжения в час", 13, Ink);
            Bar(new Rect(1082, 755, 460, 5), simulation.ShipQueued ? simulation.ShipWork / StrategySimulation.ShipCost : 0, Teal);
            bool atShipyard = !simulation.Fleet.Travelling && simulation.Fleet.Location == 1;
            string label = simulation.ShipQueued ? "Строительство идёт" : atShipyard ? "Заказать фрегат" : "Верните флот на Землю для строительства";
            if (Button(new Rect(1082, 778, 460, 33), label, false, atShipyard && !simulation.ShipQueued, 12)) simulation.QueueShip();
            Text(new Rect(1082, 821, 460, 21), "Построено: " + simulation.ShipsBuilt + "  ·  очередь: 1 корабль", 12, Muted);
            string cause = simulation.FleetCause();
            // Diagnostic strip connects a visible network problem to its downstream fleet.
            Fill(new Rect(60, 186, 951, 49), new Color(0.04f, 0.075f, 0.10f, 0.96f));
            Text(new Rect(75, 195, 920, 33), cause, 13, simulation.Fleet.NetworkSupply < simulation.Fleet.Demand ? Amber : Teal);
        }

        private void Text(Rect rect, string text, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            int key = size * 100 + (bold ? 10 : 0) + (int)align;
            if (!textStyles.TryGetValue(key, out GUIStyle style))
            {
                style = new GUIStyle { font = font, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = align, wordWrap = true, clipping = TextClipping.Clip };
                style.normal.textColor = Color.white;
                textStyles[key] = style;
            }
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        private bool Button(Rect rect, string label, bool active = false, bool enabled = true, int size = 13)
        {
            GUI.enabled = enabled;
            GUI.backgroundColor = active ? new Color(0.16f, 0.36f, 0.34f) : new Color(0.12f, 0.19f, 0.25f);
            GUI.color = enabled ? Color.white : new Color(1, 1, 1, 0.45f);
            // IMGUI is only the renderer. Pointer input comes from the Input System,
            // because OnGUI does not receive runtime pointer events with the new backend.
            Fill(rect, enabled && rect.Contains(PointerPosition()) ? new Color(0.17f, 0.27f, 0.33f) : active ? new Color(0.14f, 0.32f, 0.30f) : new Color(0.10f, 0.16f, 0.21f));
            Text(rect, label, size, enabled ? Ink : Muted, false, TextAnchor.MiddleCenter);
            bool pressed = enabled && Pressed(rect);
            GUI.enabled = true;
            GUI.backgroundColor = GUI.color = Color.white;
            return pressed;
        }

        private Vector2 PointerPosition()
        {
            if (Mouse.current == null) return new Vector2(-1000, -1000);
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 screen = Mouse.current.position.ReadValue();
            return new Vector2((screen.x - (Screen.width - 1600 * scale) / 2) / scale,
                (Screen.height - screen.y - (Screen.height - 900 * scale) / 2) / scale);
        }

        private bool Pressed(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || consumedClickFrame == Time.frameCount || Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame || !rect.Contains(PointerPosition())) return false;
            consumedClickFrame = Time.frameCount;
            return true;
        }

        private float Slider(Rect rect, float value, float min, float max, int id)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && Event.current.type == EventType.Repaint)
            {
                if (!mouse.leftButton.isPressed) activeSlider = -1;
                if (Pressed(rect)) activeSlider = id;
                if (activeSlider == id && mouse.leftButton.isPressed)
                    value = Mathf.Lerp(min, max, Mathf.Clamp01((PointerPosition().x - rect.x) / rect.width));
            }
            float ratio = Mathf.InverseLerp(min, max, value);
            Fill(new Rect(rect.x, rect.y + 6, rect.width, 4), Border);
            Fill(new Rect(rect.x, rect.y + 6, rect.width * ratio, 4), Teal);
            Fill(new Rect(rect.x + rect.width * ratio - 5, rect.y, 10, 17), Teal);
            return value;
        }

        private static string Percent(double value) => (value * 100).ToString("0") + "%";
        private static void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }
        private static void Box(Rect rect) { Fill(rect, Border); Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Panel); }
        private static void Bar(Rect rect, double value, Color color) { Fill(rect, Border); Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)value), rect.height), color); }
        private static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            // Logical-space segments remain aligned when the game view is resized.
            int segments = Mathf.CeilToInt(Vector2.Distance(a, b) / (width * 0.8f));
            for (int i = 0; i <= segments; i++)
            {
                Vector2 point = Vector2.Lerp(a, b, i / (float)segments);
                Fill(new Rect(point.x - width / 2, point.y - width / 2, width, width), color);
            }
        }

        private void GenerateTextures()
        {
            var random = new System.Random(47);
            stars = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            var pixels = new Color[512 * 256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.027f, 0.048f, 0.073f);
            for (int i = 0; i < 440; i++) { int index = random.Next(pixels.Length); float light = (float)random.NextDouble() * 0.3f + 0.12f; pixels[index] = new Color(light * 0.7f, light * 0.9f, light); }
            stars.SetPixels(pixels); stars.Apply(); stars.filterMode = FilterMode.Point;
            circle = DiscTexture(new Color(1, 1, 1), false, 0);
            planets[0] = DiscTexture(new Color(0.65f, 0.43f, 0.28f), true, 0);
            planets[1] = DiscTexture(new Color(0.18f, 0.47f, 0.65f), true, 1);
            planets[2] = DiscTexture(new Color(0.40f, 0.49f, 0.57f), true, 2);
            ship = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            var shipPixels = new Color[24 * 24];
            for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) shipPixels[y * 24 + x] = x > 3 && x < 22 && Math.Abs(y - 12) < (22 - x) * 0.5 && (x > 8 || Math.Abs(y - 12) > 2) ? Color.white : Color.clear;
            ship.SetPixels(shipPixels); ship.Apply(); ship.filterMode = FilterMode.Point;
        }

        private static Texture2D DiscTexture(Color color, bool shade, int seed)
        {
            var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            var pixels = new Color[96 * 96];
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            {
                float nx = (x - 47.5f) / 47, ny = (y - 47.5f) / 47;
                float distance = nx * nx + ny * ny;
                if (distance > 1) { pixels[y * 96 + x] = Color.clear; continue; }
                float light = shade ? Mathf.Clamp01(0.4f + Mathf.Sqrt(1 - distance) * 0.55f - nx * 0.25f + ny * 0.15f) : 1;
                float noise = shade ? 0.7f + 0.3f * Mathf.PerlinNoise(x * 0.085f + seed * 10, y * 0.085f) : 1;
                Color c = color * light * noise;
                if (shade && seed == 1 && Mathf.PerlinNoise(x * 0.06f, y * 0.07f) > 0.52f) c = new Color(0.23f, 0.48f, 0.34f) * light;
                c.a = 1; pixels[y * 96 + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply(); texture.filterMode = FilterMode.Point;
            return texture;
        }

        private IEnumerator SmokeTest()
        {
            paused = true;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--capture-dir");
            string folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "healthy.png"));
            yield return new WaitForSecondsRealtime(1);
            simulation.Routes[1].Capacity = 15;
            simulation.Advance(24);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "bottleneck.png"));
            yield return new WaitForSecondsRealtime(1);
            bool passed = simulation.Fleet.SupplyRatio < 0.5 && simulation.Fleet.Organization < 70 && runtimeErrors == 0;
            File.WriteAllText(Path.Combine(folder, "smoke-result.json"), "{\"passed\":" + (passed ? "true" : "false") + ",\"runtimeErrors\":" + runtimeErrors + "}");
            Debug.Log("Prototype smoke test: " + (passed ? "PASS" : "FAIL"));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
