﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class FloatMenuModule : Module
    {
        public Dictionary<CallInfo, bool?> FloatMenuSettings = new Dictionary<CallInfo, bool?>();
        private Vector2 scrollPos;
        public bool ShowSwitchButtons = true;
        public override string Label => "Float Menus";


        public static FloatMenuModule Instance => UIMod.GetModule<FloatMenuModule>();

        private static CallInfo GetKey()
        {
            return new StackTrace(false).GetFrames()?.Skip(3).First(frame => !SubclassOrEqual(frame.GetMethod().DeclaringType, typeof(FloatMenu)));
        }

        private static bool SubclassOrEqual(Type type1, Type type2)
        {
            return type1 == type2 || type1.IsSubclassOf(type2);
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)), new HarmonyMethod(typeof(FloatMenuModule), nameof(AddPrefix)));
            harm.Patch(AccessTools.Constructor(typeof(FloatMenu), new[] {typeof(List<FloatMenuOption>)}), new HarmonyMethod(typeof(FloatMenuModule), nameof(AddSwitchOption)));
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var outRect = new Rect(inRect.ContractedBy(15f));
            outRect.yMin += 10f;
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Instance.FloatMenuSettings.Count * 150f + 50f);
            var listing = new Listing_Standard();
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            listing.Begin(viewRect);
            listing.CheckboxLabeled("Show Switch Buttons", ref Instance.ShowSwitchButtons);
            foreach (var setting in Instance.FloatMenuSettings.ToList())
            {
                listing.Label(setting.Key);
                listing.Gap(4f);
                listing.ColumnWidth -= 12f;
                listing.Indent();
                if (listing.RadioButton("Force Dialog", setting.Value.HasValue && setting.Value.Value)) Instance.FloatMenuSettings[setting.Key] = true;
                if (listing.RadioButton("Force Vanilla", setting.Value.HasValue && !setting.Value.Value)) Instance.FloatMenuSettings[setting.Key] = false;
                if (listing.RadioButton("Default (Dialog when >30 options)", !setting.Value.HasValue)) Instance.FloatMenuSettings[setting.Key] = null;
                listing.ColumnWidth += 12f;
                listing.Outdent();
                listing.GapLine();
            }

            listing.End();
            Widgets.EndScrollView();
        }

        public static bool AddPrefix(WindowStack __instance, Window window)
        {
            if (window is FloatMenu menu)
            {
                var key = GetKey();
                if (!Instance.FloatMenuSettings.ContainsKey(key)) Instance.FloatMenuSettings.Add(key, null);
                var res = Instance.FloatMenuSettings[key];
                if (!res.HasValue && menu.options.Count > 30 || res.HasValue && res.Value)
                {
                    __instance.Add(new Dialog_FloatMenuOptions(menu.options, key));
                    return false;
                }
            }

            return true;
        }

        public static void AddSwitchOption(List<FloatMenuOption> options)
        {
            var key = GetKey();
            if (Instance.ShowSwitchButtons &&
                !(Instance.FloatMenuSettings.ContainsKey(key) && Instance.FloatMenuSettings[key].HasValue && Instance.FloatMenuSettings[key].Value) &&
                key.MethodName != "TryMakeFloatMenu")
                options.Add(new FloatMenuOption("Switch to Full Dialog", () => Instance.FloatMenuSettings[key] = true));
        }

        public override void SaveSettings()
        {
            Scribe_Collections.Look(ref FloatMenuSettings, "floatMenu", LookMode.Deep, LookMode.Value);
            Scribe_Values.Look(ref ShowSwitchButtons, "showSwitch", true);
            if (FloatMenuSettings == null) FloatMenuSettings = new Dictionary<CallInfo, bool?>();
        }

        public struct CallInfo : IExposable
        {
            public string TypeName;
            public string Namespace;
            public string MethodName;

            public static implicit operator string(CallInfo info)
            {
                return $"{info.Namespace}.{info.TypeName}.{info.MethodName}";
            }

            public static implicit operator CallInfo(StackFrame frame)
            {
                return new CallInfo(frame);
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref MethodName, "method");
                Scribe_Values.Look(ref TypeName, "type");
                Scribe_Values.Look(ref Namespace, "namespace");
            }

            public CallInfo(StackFrame frame)
            {
                var method = frame.GetMethod();
                MethodName = method.Name;
                TypeName = method.DeclaringType?.Name;
                Namespace = method.DeclaringType?.Namespace;
            }

            public bool Valid => !MethodName.NullOrEmpty() && !TypeName.NullOrEmpty();
        }
    }
}