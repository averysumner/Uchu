using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Uchu.Core;
using Uchu.Python;

namespace Uchu.World.Scripting.Managed
{
    public class PythonScript : Script
    {
        public string Source { get; }
        
        public Zone Zone { get; }
        
        public ManagedScript Script { get; set; }
        
        public Object Proxy { get; set; }
        
        public PythonScript(string source, Zone zone)
        {
            Source = source;
            
            Zone = zone;
        }

        public override Task LoadAsync()
        {
            Proxy = Object.Instantiate(Zone);

            Object.Start(Proxy);
            
            Script = new ManagedScript(
                Source,
                Zone.ManagedScriptEngine
            );

            var variables = new Dictionary<string, dynamic>
            {
                ["Zone"] = Zone,
                ["Start"] = new Action<Object>(Object.Start),
                ["Destroy"] = new Action<Object>(Object.Destroy),
                ["Construct"] = new Action<GameObject>(GameObject.Construct),
                ["Serialize"] = new Action<GameObject>(GameObject.Serialize),
                ["Destruct"] = new Action<GameObject>(GameObject.Destruct),
                ["Create"] = new Func<int, Vector3, Quaternion, GameObject>
                    ((lot, position, rotation) => GameObject.Instantiate(Zone, lot, position, rotation)),
                ["Broadcast"] = new Action<dynamic>(obj =>
                {
                    foreach (var player in Zone.Players)
                    {
                        player.SendChatMessage(obj, PlayerChatChannel.Normal);
                    }
                }),
                ["OnTick"] = new Action<GameObject, Action>((gameObject, action) => Listen(gameObject.OnTick, action)),
                ["OnStart"] = new Action<GameObject, Action>((gameObject, action) => Listen(gameObject.OnStart, action)),
                ["OnDestroy"] = new Action<GameObject, Action>((gameObject, action) => Listen(gameObject.OnDestroyed, action)),
                ["OnInteract"] = new Action<GameObject, Action<Player>>((gameObject, action) => Listen(gameObject.OnInteract, action)),
                ["OnHealth"] = new Action<GameObject, Action<int, int, GameObject>>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return;

                    Listen(stats.OnHealthChanged, (newHealth, delta) =>
                    {
                        action((int) newHealth, delta, stats.LatestDamageSource);
                        
                        return Task.CompletedTask;
                    });
                }),
                ["OnArmor"] = new Action<GameObject, Action<int, int, GameObject>>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return;

                    Listen(stats.OnArmorChanged, (newArmor, delta) =>
                    {
                        action((int) newArmor, delta, stats.LatestDamageSource);
                        
                        return Task.CompletedTask;
                    });
                }),
                ["OnDeath"] = new Action<GameObject, Action<GameObject>>((gameObject, action) =>
                {
                    if (!gameObject.TryGetComponent<Stats>(out var stats)) return;
                    
                    Listen(stats.OnDeath, () => { action(stats.LatestDamageSource); });
                }),
                ["Drop"] = new Action<int, Vector3, GameObject, Player>((lot, position, source, player) =>
                {
                    var loot = InstancingUtil.Loot(lot, player, source, position);

                    Object.Start(loot);
                }),
                ["Currency"] = new Action<int, Vector3, GameObject, Player>((count, position, source, player) =>
                {
                    InstancingUtil.Currency(count, player, source, position);
                }),
                ["GetComponent"] = new Func<GameObject, string, Component>((gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");
                    
                    return type != default ? gameObject.GetComponent(type) : null;
                }),
                ["AddComponent"] = new Func<GameObject, string, Component>((gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");
                    
                    return type != default ? gameObject.AddComponent(type) : null;
                }),
                ["RemoveComponent"] = new Action<GameObject, string>((gameObject, name) =>
                {
                    var type = Type.GetType($"Uchu.World.{name}");

                    if (type == null) return;
                    
                    gameObject.RemoveComponent(type);
                }),
                ["Vector3"] = new Func<float, float, float, Vector3>((x, y, z) => new Vector3(x, y, z)),
                ["Quaternion"] = new Func<float, float, float, float, Quaternion>((x, y, z, w) => new Quaternion(x, y, z, w))
            };

            Script.Run(variables.ToArray());
            
            Script.Execute("load");
            
            Listen(Proxy.OnTick, () =>
            {
                Script.Execute("tick");
            });

            return Task.CompletedTask;
        }

        public override Task UnloadAsync()
        {
            Object.Destroy(Proxy);
            
            ClearListeners();

            Script.Execute("unload");
            
            return Task.CompletedTask;
        }
    }
}