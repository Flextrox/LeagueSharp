using System;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

namespace Twitch
{
    public class Program
    {
        private static Menu config;
        private static Orbwalking.Orbwalker orbwalker;

        private static Spell q;
        private static Spell w;
        private static Spell e;
        private static Spell r;
        private static Spell recall;

        private static Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }
        
        private static float GetDamage(Obj_AI_Hero target)
        {
            return e.GetDamage(target);
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            //Verify Champion
            if (Player.ChampionName != "Twitch")
                return;

            //Spells
            q = new Spell(SpellSlot.Q);
            w = new Spell(SpellSlot.W, 950);
            w.SetSkillshot(0.25f, 120f, 1400f, false, SkillshotType.SkillshotCircle);
            e = new Spell(SpellSlot.E, 1200);
            recall = new Spell(SpellSlot.Recall);

            //Menu instance
            config = new Menu("Twitch", "Twitch", true);

            //Orbwalker
            orbwalker = new Orbwalking.Orbwalker(config.SubMenu("Orbwalking"));

            //Targetsleector
            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            config.AddSubMenu(targetSelectorMenu);

            //Combo
            config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true));
            config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true));
            config.SubMenu("Combo").AddItem(new MenuItem("UseECombo", "Use E").SetValue(true));

            //Misc
            config.SubMenu("Misc").AddItem(new MenuItem("Emobs", "Kill mobs with E").SetValue(new StringList(new[] { "Baron + Dragon + Siege Minion", "Baron + Dragon", "None" })));
            config.SubMenu("Misc").AddItem(new MenuItem("stealthrecall", "Stealth Recall", true).SetValue(new KeyBind('T', KeyBindType.Press)));
            config.SubMenu("Misc").AddItem(new MenuItem("blueTrinket", "Buy Blue Trinket").SetValue(true));

            //Items
            config.SubMenu("Items").AddItem(new MenuItem("bladeBoss", "BotRK").SetValue(true));
            config.SubMenu("Items").AddItem(new MenuItem("ghostBoss", "Ghostblade").SetValue(true));

            //Drawings
            config.SubMenu("Drawing").AddItem(new MenuItem("EDamage", "E Damage").SetValue(new Circle(true, Color.Green)));

            //Attach to root
            config.AddToMainMenu();

            // Enable E damage indicators
            CustomDamageIndicator.Initialize(GetDamage);

            //Listen to events
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            // E damage on healthbar
            CustomDamageIndicator.DrawingColor = config.Item("EDamage").GetValue<Circle>().Color;
            CustomDamageIndicator.Enabled = config.Item("EDamage").GetValue<Circle>().Active;

        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (e.IsReady())
            {

                //Kill large monsters
                if (orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo)
                {
                    var minions = MinionManager.GetMinions(e.Range, MinionTypes.All, MinionTeam.NotAlly);
                    foreach (var m in minions)
                    {
                        switch (config.Item("Emobs").GetValue<StringList>().SelectedIndex)
                        {
                            case 0:
                                if ((m.BaseSkinName.Contains("MinionSiege") || m.BaseSkinName.Contains("Dragon") || m.BaseSkinName.Contains("Baron")) && e.IsKillable(m))
                                {
                                    e.Cast();
                                }
                                break;

                            case 1:
                                if ((m.BaseSkinName.Contains("Dragon") || m.BaseSkinName.Contains("Baron")) && e.IsKillable(m))
                                {
                                    e.Cast();
                                }
                                break;

                            case 2:
                                return;
                                break;
                        }
                    }
                }
            }

            //Combo
            if (orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                var target = TargetSelector.GetTarget(w.Range, TargetSelector.DamageType.Physical);

                //Items
                
                //BotrK
                if (config.Item("bladeBoss").GetValue<bool>())
                {
                    if (target != null && target.Type == Player.Type &&
                        target.ServerPosition.Distance(Player.ServerPosition) < 450)
                {
                    var hasCutGlass = Items.HasItem(3144);
                    var hasBotrk = Items.HasItem(3153);

                    if (hasBotrk || hasCutGlass)
                    {
                        var itemId = hasCutGlass ? 3144 : 3153;
                        var damage = Player.GetItemDamage(target, Damage.DamageItems.Botrk);
                        if (hasCutGlass || Player.Health + damage < Player.MaxHealth)
                            Items.UseItem(itemId, target);
                    }
                }
                }

                if (config.Item("ghostBoss").GetValue<bool>())
                {
                    if (target != null && target.Type == Player.Type && Orbwalking.InAutoAttackRange(target))
                    {
                        Items.UseItem(3142);
                    }
                }

                //Use W
                if (config.Item("UseWCombo").GetValue<bool>())
                {
                    if (target.IsValidTarget(w.Range) && w.CanCast(target))
                    {
                        w.Cast(target);
                    }
            
                }

                //Use Q
                if (config.Item("UseQCombo").GetValue<bool>())
                {
                    if (target.IsValidTarget(q.Range) && q.CanCast(target))
                    {
                        q.Cast(target);
                    }
                } 
                
                //Use E
                if (config.Item("UseECombo").GetValue<bool>())
                {

                    foreach (
                        var enemy in
                            ObjectManager.Get<Obj_AI_Hero>()
                                .Where(enemy => enemy.IsValidTarget(e.Range) && e.IsKillable(enemy))
                        )
                    {
                        e.Cast();
                    }
                }
            }

            //Recall

            if (config.Item("stealthrecall", true).GetValue<KeyBind>().Active)
            {
                if (q.IsReady() && recall.IsReady())
                {
                    q.Cast();
                    recall.Cast();
                }
            }

            //Blue Trinket
            if (config.Item("blueTrinket").GetValue<bool>() && Player.Level >= 6 && Player.InShop() && !(Items.HasItem(3342) || Items.HasItem(3363)))
            {
                Player.BuyItem(ItemId.Scrying_Orb_Trinket);
            }
            

        }
    }
}
