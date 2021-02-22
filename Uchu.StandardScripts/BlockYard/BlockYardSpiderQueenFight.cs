﻿using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Uchu.Core.Client;
using Uchu.Core;
using Uchu.World;
using Uchu.World.Scripting.Native;
using Uchu.StandardScripts;
using System.IO;
using System;
using System.Collections.Generic;
using InfectedRose.Luz;
using System.Numerics;
using System.Timers;
using IronPython.Modules;
using Uchu.Core.Resources;
using Uchu.World.Client;
using DestructibleComponent = Uchu.World.DestructibleComponent;
using Object = Uchu.World.Object;

namespace Uchu.StandardScripts.BlockYard
{
    [ZoneSpecific(1150)]
    public class BlockYardSpiderQueenFight : NativeScript
    {
        /// <summary>
        /// Ambient sound when maelstrom battle is active
        /// </summary>
        private static readonly Guid GuidMaelstrom = new Guid("{7881e0a1-ef6d-420c-8040-f59994aa3357}");
        
        /// <summary>
        /// Ambient sound when maelstrom battle is over
        /// </summary>
        private static readonly Guid GuidPeaceful = new Guid("{c5725665-58d0-465f-9e11-aeb1d21842ba}");

        /// <summary>
        /// Objects that are always present
        /// </summary>
        private static readonly HashSet<string> GlobalObjects = new HashSet<string> {
            "Mailbox",
            "PropertyGuard",
            "Launcher"
        };
        
        /// <summary>
        /// Objects needed for maelstrom battle
        /// </summary>
        private static readonly HashSet<string> MaelstromObjects = new HashSet<string> {
            "DestroyMaelstrom",
            "SpiderBoss",
            "SpiderEggs",
            "Rocks",
            "DesMaelstromInstance",
            "Spider_Scream",
            "ROF_Targets_00",
            "ROF_Targets_01",
            "ROF_Targets_02",
            "ROF_Targets_03",
            "ROF_Targets_04"
        };
        
        /// <summary>
        /// Objects needed once Maelstrom battle is over
        /// </summary>
        private static readonly HashSet<string> PeacefulObjects = new HashSet<string> {
            "SunBeam",
            "BirdFX",
            "BankObj",
            "AGSmallProperty"
        };

        /// <summary>
        /// Whether the player has started the fight yet
        /// </summary>
        private bool _fightStarted;

        /// <summary>
        /// Whether the player has completed the fight yet
        /// </summary>
        private bool _fightCompleted;

        /// <summary>
        /// Whether the global objects have been spawned
        /// </summary>
        private bool _globalSpawned;

        private bool _peacefulSpawned;

        public override Task LoadAsync()
        {
            Listen(Zone.OnPlayerLoad, async player =>
            {
                _fightCompleted = player.GetComponent<CharacterComponent>().GetFlag(FlagId.BeatSpiderQueen);
                
                if (!_globalSpawned)
                {
                    SpawnGlobal();
                    _globalSpawned = true;
                }
                
                if (_fightCompleted && !_peacefulSpawned)
                {
                    SpawnPeaceful(player);
                    _peacefulSpawned = true;
                }
                else if (!_fightCompleted && !_fightStarted)
                {
                    SpawnMaelstrom(player);

                    var spiderQueen = Zone.GameObjects.First(go => go.Lot == Lot.SpiderQueen);
                    var spiderEggSpawner = Zone.GameObjects.First(go => go.Name == "SpiderEggs");
                    
                    var spiderQueenFight = await SpiderQueenFight.Instantiate(Zone, spiderQueen, spiderEggSpawner, 
                        new List<Player> { player });
                    spiderQueenFight.StartFight();
                    _fightStarted = true;
                    
                    Listen(spiderQueenFight.OnFightCompleted, () =>
                    {
                        player.GetComponent<CharacterComponent>().SetFlagAsync(FlagId.BeatSpiderQueen, true);
                        _fightCompleted = true;
                        _peacefulSpawned = true;
                        SpawnPeaceful(player);
                    });
                }
            });

            return Task.CompletedTask;
        }
        
        private void SpawnGlobal()
        {
            foreach (var path in Zone.ZoneInfo.LuzFile.PathData.OfType<LuzSpawnerPath>()
                .Where(p => GlobalObjects.Contains(p.PathName)))
            {
                Spawn(path);
            }
            
            Logger.Debug("Spawned global");
        }
            
        private void SpawnMaelstrom(Player player)
        {
            StartFightEffects(player);
            
            // Destroy all maelstrom spawners
            foreach (var gameObject in Zone.GameObjects.Where(go => PeacefulObjects.Contains(go.Name)).ToArray())
            {
                // Destroy all spawned objects, except the spider queen which will be automatically destroyed afterwards
                if (gameObject.TryGetComponent<SpawnerComponent>(out var spawner))
                    foreach (var spawnedObject in spawner.ActiveSpawns.ToArray())
                        Destroy(spawnedObject);
                Destroy(gameObject);
            }
            
            foreach (var path in Zone.ZoneInfo.LuzFile.PathData.OfType<LuzSpawnerPath>()
                .Where(p => MaelstromObjects.Contains(p.PathName)))
            {
                Spawn(path);
            }
            
            Logger.Debug("Spawned maelstrom");
        }

        private void SpawnPeaceful(Player player)
        {
            StopFightEffects(player);
            
            // Destroy all maelstrom spawners
            foreach (var gameObject in Zone.GameObjects.Where(go => MaelstromObjects.Contains(go.Name)).ToArray())
            {
                // Destroy all spawned objects, except the spider queen which will be automatically destroyed afterwards
                if (gameObject.TryGetComponent<SpawnerComponent>(out var spawner) && gameObject.Name != "SpiderBoss")
                    foreach (var spawnedObject in spawner.ActiveSpawns.ToArray())
                        Destroy(spawnedObject);
                Destroy(gameObject);
            }
            
            // Create all peaceful spawners
            foreach (var path in Zone.ZoneInfo.LuzFile.PathData.OfType<LuzSpawnerPath>()
                .Where(p => PeacefulObjects.Contains(p.PathName)))
            {
                Spawn(path);
            }
            
            Logger.Debug("Spawned peaceful");
        }

        private void Spawn(LuzSpawnerPath path)
        {
            var gameObject = InstancingUtilities.Spawner(path, Zone);
            if (gameObject == null)
                return;
            
            gameObject.Layer = StandardLayer.Hidden;

            var spawner = gameObject.GetComponent<SpawnerComponent>();
            spawner.SpawnsToMaintain = (int)path.NumberToMaintain;
            spawner.SpawnLocations = path.Waypoints.Select(w => new SpawnLocation
            {
                Position = w.Position,
                Rotation = Quaternion.Identity
            }).ToList();

            Start(gameObject);
            spawner.SpawnCluster();
        }
        
        private void StartFightEffects(Player player)
        {
            var maelStromFxObject = Zone.GameObjects.First(go => go.Lot == Lot.TornadoBgFx);
            
            player.Message(new PlayNDAudioEmitterMessage
            {
                Associate = player,
                NDAudioEventGUID = GuidMaelstrom.ToString()
            });

            
            player.Message(new PlayFXEffectMessage
            {
                Name = "TornadoDebris",
                EffectType = "debrisOn",
                Associate = maelStromFxObject
            });
                    
            player.Message(new PlayFXEffectMessage
            {
                Name = "TornadoVortex",
                EffectType = "VortexOn",
                Associate = maelStromFxObject
            });
                    
            player.Message(new PlayFXEffectMessage
            {
                Name = "silhouette",
                EffectType = "onSilhouette",
                Associate = maelStromFxObject
            });
        }

        private void StopFightEffects(Player player)
        {
            var maelStromFxObject = Zone.GameObjects.First(go => go.Lot == Lot.TornadoBgFx);
            
            player.Message(new PlayNDAudioEmitterMessage
            {
                Associate = player,
                NDAudioEventGUID = GuidPeaceful.ToString()
            });
            
            player.Message(new PlayFXEffectMessage
            {
                Name = "TornadoDebris",
                EffectType = "debrisOff",
                Associate = maelStromFxObject
            });
                    
            player.Message(new PlayFXEffectMessage
            {
                Name = "TornadoVortex",
                EffectType = "VortexOff",
                Associate = maelStromFxObject
            });
                    
            player.Message(new PlayFXEffectMessage
            {
                Name = "silhouette",
                EffectType = "offSilhouette",
                Associate = maelStromFxObject
            });
            
            Destroy(maelStromFxObject);
        }
        
        /// <summary>
        /// Represents one spider queen boss fight
        /// </summary>
        public class SpiderQueenFight : Object
        {
            public static async Task<SpiderQueenFight> Instantiate(Zone zone, GameObject spiderQueen, GameObject spiderEggSpawner,
                List<Player> players)
            {
                var instance = Instantiate<SpiderQueenFight>(zone);
                instance._players = players;
                instance._spiderEggSpawner = spiderEggSpawner;

                instance._spiderQueenFactions = spiderQueen.GetComponent<DestroyableComponent>().Factions.ToArray();
                instance._stage2Threshold = spiderQueen.GetComponent<DestroyableComponent>().MaxHealth / 3 * 2;
                instance._stage3Threshold = spiderQueen.GetComponent<DestroyableComponent>().MaxHealth / 3;
                instance._spiderQueen = spiderQueen;
                
                instance._stage2SpiderlingCount = 2;
                instance._stage3SpiderlingCount = 3;
                instance._spawnedSpiderlings = new List<GameObject>();
                instance._preppedSpiderEggs = new List<GameObject>();

                // Cache all the animation times for animations executed by the spider queen
                instance._tauntAnimationTime = await GetAnimationTimeAsync("taunt");
                instance._rainOfFireAnimationTime = await GetAnimationTimeAsync("attack-fire");
                instance._withdrawalTime = await GetAnimationTimeAsync("withdraw");
                instance._advanceAnimationTime = await GetAnimationTimeAsync("advance");
                instance._withdrawnIdleAnimationTime = await GetAnimationTimeAsync("idle-withdrawn");
                instance._shootLeftAnimationTime = await GetAnimationTimeAsync("attack-shoot-left");
                instance._shootRightAnimationTime = await GetAnimationTimeAsync("attack-shoot-right");
                instance._shootAnimationTime = await GetAnimationTimeAsync("attack-fire-single");
                
                return instance;
            }

            private static async Task<float> GetAnimationTimeAsync(string animationName)
            {
                var animationTable = await ClientCache.GetTableAsync<Animations>();
                var advanceAnimationLength = animationTable.First(
                    a => a.Animationtype == animationName && a.AnimationGroupID == 541
                    ).Animationlength;
                return (advanceAnimationLength ?? 0) * 1000;
            }

            private SpiderQueenFight()
            {
                OnFightCompleted = new Event();
            }
            
            /// <summary>
            /// Event called when the player completes the fight
            /// </summary>
            public Event OnFightCompleted { get; set; }

            /// <summary>
            /// The participant in this spider queen fight
            /// </summary>
            private List<Player> _players;

            /// <summary>
            /// The spider queen currently active for the participants in the fight
            /// </summary>
            private GameObject _spiderQueen;

            private int[] _spiderQueenFactions;

            private GameObject _spiderEggSpawner;

            private List<GameObject> _spawnedSpiderlings;
            private List<GameObject> _preppedSpiderEggs;
            private int _spiderEggsToPrep;

            private uint _stage = 1;
            private bool _withdrawn;
            private uint _stage2Threshold;
            private uint _stage3Threshold;
            private int _stage2SpiderlingCount;
            private int _stage3SpiderlingCount;
            private int _killedSpiders;
            private float _withdrawalTime;
            private float _advanceAnimationTime;
            private GameObject _advanceAttackTarget;
            private float _tauntAnimationTime;
            private float _rainOfFireAnimationTime;
            private float _withdrawnIdleAnimationTime;
            private float _shootLeftAnimationTime;
            private float _shootRightAnimationTime;
            private float _shootAnimationTime;
            private int _currentSpiderlingWavecount;


            #region state
            /// <summary>
            /// Starts the spider queen fight with the participants
            /// </summary>
            public void StartFight()
            {
                Logger.Information("Starting spider queen fight!");
                
                // Stop the fight if the spider queen was killed
                Listen(_spiderQueen.GetComponent<DestroyableComponent>().OnHealthChanged, async 
                    (newHealth, delta) =>
                {
                    var impliedStage = newHealth < _stage3Threshold ? 3 
                        : newHealth < _stage2Threshold ? 2 : 1;
                    if (impliedStage > _stage)
                        WithdrawSpiderQueen();

                    if (newHealth <= 0)
                        await OnFightCompleted.InvokeAsync();
                });

                // Listen to smashed spiderlings to update the spiderling wave
                Listen(Zone.OnObject, o =>
                {
                    if (o is GameObject spiderling && spiderling.Lot == Lot.SpiderQueenSpiderling)
                    {
                        _spawnedSpiderlings.Add(spiderling);
                        Listen(spiderling.GetComponent<DestroyableComponent>().OnDeath, 
                            () => HandleSpiderlingDeath(spiderling));
                    }
                });

                foreach (var player in _players)
                {
                    Listen(player.OnFireServerEvent, (name, message) =>
                    {
                        if (message.Arguments == "CleanupSpiders")
                            CleanupSpiders();
                    });
                }
            }

            #endregion state

            #region ai

            private void AdvanceSpiderQueen()
            {
                if (!_withdrawn)
                    return;
                
                Logger.Information("Advancing spider queen!");
                
                _spiderQueen.Animate("advance");
                Zone.Schedule(AdvanceAttack, _advanceAnimationTime - 400);
                Zone.Schedule(AdvanceComplete, _advanceAnimationTime);

                _withdrawn = false;
            }

            private void HandleSpiderlingDeath(GameObject spiderling)
            {
                Logger.Information($"{spiderling} was smashed!");
                
                _killedSpiders++;
                _spawnedSpiderlings.Remove(spiderling);
                
                if (_killedSpiders >= _currentSpiderlingWavecount)
                    AdvanceSpiderQueen();
            }

            private void AdvanceAttack()
            {
                Logger.Information("Spider queen advance attack!");
                
                if (_advanceAttackTarget != null)
                {
                    // TODO
                }
            }

            private void AdvanceComplete()
            {
                Logger.Information("Spider queen completed advance!");
                
                _spiderQueen.GetComponent<DestroyableComponent>().Factions = _spiderQueenFactions;
                _stage += 1;
                _killedSpiders = 0;
                _currentSpiderlingWavecount = 0;
                _spiderQueen.Animate("taunt");
                Zone.Schedule(AdvanceTauntComplete, _advanceAnimationTime);
            }

            private void AdvanceTauntComplete()
            {
                // TODO: Special skills delay
                Logger.Information("Spider queen advance taunt has completed!");
                
                // Reset immunity
                Zone.BroadcastMessage(new SetStatusImmunityMessage
                {
                    Associate = _spiderQueen,
                    ImmunityState = ImmunityState.Pop,
                    ImmuneToSpeed = true,
                    ImmuneToBasicAttack = true,
                    ImmuneToDOT = true
                });
                
                Zone.BroadcastMessage(new SetStunnedMessage
                {
                    Associate = _spiderQueen,
                    StunState = StunState.Pop,
                    CantMove = true,
                    CantJump = true,
                    CantAttack = true,
                    CantTurn = true,
                    CantUseItem = true,
                    CantEquip = true,
                    CantInteract = true,
                    IgnoreImmunity = true
                });
            }
            
            private void WithdrawSpiderQueen()
            {
                if (_withdrawn)
                    return;
                
                Logger.Information("Withdrawing spider queen!");

                // Spider queen is immune to any attacks during the withdrawn phase
                Zone.BroadcastMessage(new SetStunnedMessage
                {
                    Associate = _spiderQueen,
                    StunState = StunState.Push,
                    CantMove = true,
                    CantJump = true,
                    CantAttack = true,
                    CantTurn = true,
                    CantUseItem = true,
                    CantEquip = true,
                    CantInteract = true,
                    IgnoreImmunity = true
                });

                // Orientation for the animation to make sense
                _spiderQueen.Transform.Rotate(new Quaternion { X = 0.0f, Y = -0.005077f, Z = 0.0f, W = 0.999f });
                _spiderQueen.Animate("withdraw");
                _spiderQueen.GetComponent<DestroyableComponent>().Factions = new int[] {};
                
                Zone.BroadcastMessage(new SetStatusImmunityMessage
                {
                    Associate = _spiderQueen,
                    ImmunityState = ImmunityState.Push,
                    ImmuneToSpeed = true,
                    ImmuneToBasicAttack = true,
                    ImmuneToDOT = true
                });

                Zone.Schedule(WithdrawalComplete, _withdrawalTime - 250);
                _withdrawn = true;
            }

            private void WithdrawalComplete()
            {
                Logger.Information("Spider queen is withdrawn!");
                
                _spiderQueen.Animate("idle-withdrawn");
                _currentSpiderlingWavecount = _stage == 1 ? _stage2SpiderlingCount : _stage3SpiderlingCount;
                _spiderEggsToPrep = _currentSpiderlingWavecount;
                SpawnSpiders();
            }
            
            #region spiderwave
                        
            /// <summary>
            /// Removes all the spiders from the scene
            /// </summary>
            private void CleanupSpiders()
            {
                foreach (var spiderling in _spawnedSpiderlings)
                {
                    Zone.BroadcastMessage(new DieMessage
                    {
                        Associate = spiderling,
                        Killer = _spiderQueen,
                        KillType = 1
                    });
                }
                
                _spawnedSpiderlings = new List<GameObject>();
            }

            private void SpawnSpiders()
            {
                Logger.Information("Spawning spiders!");
                
                var spiderEggs = _spiderEggSpawner.GetComponent<SpawnerComponent>().ActiveSpawns
                    .Except(_preppedSpiderEggs).ToList();
                
                // If no spider eggs are available, try again in a second
                if (spiderEggs.Count <= 0)
                {
                    Zone.Schedule(SpawnSpiders, 1000);
                    return;
                }
                
                var rng = new Random();
                var newlyPreppedEggs = 0;
                
                for (var i = 0; i < _spiderEggsToPrep; i++)
                {
                    var eggToPrep = spiderEggs[rng.Next(0, spiderEggs.Count)];
                    spiderEggs.Remove(eggToPrep);
                    _preppedSpiderEggs.Add(eggToPrep);
                    
                    Logger.Information($"Prepping {eggToPrep}");
                    
                    Zone.BroadcastMessage(new FireClientEventMessage
                    {
                        Arguments = "prepEgg",
                        Target = eggToPrep,
                        Sender = _spiderQueen
                    });
                    
                    newlyPreppedEggs++;
                }
                
                _spiderEggsToPrep -= newlyPreppedEggs;
                
                // There weren't enough spider eggs to hatch, try again in a second
                if (_spiderEggsToPrep > 0)
                {
                    Zone.Schedule(SpawnSpiders, 1000);
                }
                else
                {
                    Logger.Information("Successfully spawned spiders!");
                    
                    foreach (var eggToHatch in _preppedSpiderEggs)
                    {
                        Logger.Information($"Hatching {eggToHatch}");
                        
                        Zone.BroadcastMessage(new FireClientEventMessage
                        {
                            Arguments = "hatchEgg",
                            Target = eggToHatch,
                            Sender = _spiderQueen
                        });
                    }
                    _preppedSpiderEggs = new List<GameObject>();
                }
            }
            #endregion spiderwave
            #endregion ai
        }
    }
}