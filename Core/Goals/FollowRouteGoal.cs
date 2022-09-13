using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using SharedLib.Extensions;

#pragma warning disable 162

namespace Core.Goals
{
    public sealed class FollowRouteGoal : GoapGoal, IGoapEventListener, IRouteProvider, IEditedRouteReceiver, IDisposable
    {
        public override float Cost => 20f;

        private const bool debug = false;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly NpcNameFinder npcNameFinder;
        private readonly ClassConfiguration classConfig;
        private readonly MountHandler mountHandler;
        private readonly Navigation navigation;

        private readonly IBlacklist targetBlacklist;
        private readonly TargetFinder targetFinder;
        private const int minMs = 500, maxMs = 1000;
        private const NpcNames NpcNameToFind = NpcNames.Enemy | NpcNames.Neutral;

        private const int MIN_TIME_TO_START_CYCLE_PROFESSION = 5000;
        private const int CYCLE_PROFESSION_PERIOD = 8000;

        private readonly ManualResetEvent sideActivityManualReset;
        private readonly Thread? sideActivityThread;
        private CancellationTokenSource sideActivityCts;

        private Vector3[] mapRoute;

        private bool shouldMount;

        private DateTime onEnterTime;

        #region IRouteProvider

        public DateTime LastActive => navigation.LastActive;

        public Vector3[] PathingRoute()
        {
            return navigation.TotalRoute;
        }

        public bool HasNext()
        {
            return navigation.HasNext();
        }

        public Vector3 NextMapPoint()
        {
            return navigation.NextMapPoint();
        }

        #endregion


        public FollowRouteGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, ClassConfiguration classConfig, Vector3[] route, Navigation navigation, MountHandler mountHandler, NpcNameFinder npcNameFinder, TargetFinder targetFinder, IBlacklist blacklist)
            : base(nameof(FollowRouteGoal))
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.classConfig = classConfig;
            this.playerReader = addonReader.PlayerReader;
            this.mapRoute = route;
            this.npcNameFinder = npcNameFinder;
            this.mountHandler = mountHandler;
            this.targetFinder = targetFinder;
            this.targetBlacklist = blacklist;

            this.navigation = navigation;
            navigation.OnPathCalculated += Navigation_OnPathCalculated;
            navigation.OnDestinationReached += Navigation_OnDestinationReached;
            navigation.OnWayPointReached += Navigation_OnWayPointReached;

            if (classConfig.Mode == Mode.AttendedGather)
            {
                AddPrecondition(GoapKey.dangercombat, false);
                navigation.OnAnyPointReached += Navigation_OnWayPointReached;
            }
            else
            {
                if (classConfig.Loot)
                {
                    AddPrecondition(GoapKey.incombat, false);
                }

                AddPrecondition(GoapKey.damagedone, false);
                AddPrecondition(GoapKey.damagetaken, false);

                AddPrecondition(GoapKey.producedcorpse, false);
                AddPrecondition(GoapKey.consumecorpse, false);
            }

            sideActivityCts = new();
            sideActivityManualReset = new(false);

            if (classConfig.Mode == Mode.AttendedGather)
            {
                if (classConfig.GatherFindKeyConfig.Length > 1)
                {
                    sideActivityThread = new(Thread_AttendedGather);
                    sideActivityThread.Start();
                }
            }
            else
            {
                sideActivityThread = new(Thread_LookingForTarget);
                sideActivityThread.Start();
            }
        }

        public void Dispose()
        {
            navigation.Dispose();

            sideActivityCts.Cancel();
            sideActivityManualReset.Set();
        }

        private void Abort()
        {
            if (!targetBlacklist.Is())
                navigation.StopMovement();

            navigation.Stop();

            sideActivityManualReset.Reset();
            targetFinder.Reset();
        }

        private void Resume()
        {
            onEnterTime = DateTime.UtcNow;

            if (sideActivityCts.IsCancellationRequested)
            {
                sideActivityCts = new();
            }
            sideActivityManualReset.Set();

            if (!navigation.HasWaypoint())
            {
                RefillWaypoints(true);
            }
            else
            {
                navigation.Resume();
            }

            if (!shouldMount &&
                classConfig.UseMount &&
                mountHandler.CanMount() &&
                mountHandler.ShouldMount(navigation.TotalRoute.Last()))
            {
                shouldMount = true;
                Log("Mount up since desination far away");
            }
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is AbortEvent)
            {
                Abort();
            }
            else if (e is ResumeEvent)
            {
                Resume();
            }
        }

        public override void OnEnter() => Resume();

        public override void OnExit() => Abort();

        public override void Update()
        {
            if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
            {
                Log("Has target but its dead.");
                input.ClearTarget();
                wait.Update();
            }

            if (playerReader.Bits.IsDrowning())
            {
                input.Jump();
            }

            if (playerReader.Bits.PlayerInCombat() && classConfig.Mode != Mode.AttendedGather) { return; }

            if (!sideActivityCts.IsCancellationRequested)
                navigation.Update(sideActivityCts.Token);

            RandomJump();

            wait.Update();
        }

        private void Thread_LookingForTarget()
        {
            sideActivityManualReset.WaitOne();

            while (!sideActivityCts.IsCancellationRequested)
            {
                wait.Update();

                if (!input.Proc.IsKeyDown(input.Proc.TurnLeftKey) &&
                    !input.Proc.IsKeyDown(input.Proc.TurnRightKey) &&
                    classConfig.TargetNearestTarget.MillisecondsSinceLastClick > Random.Shared.Next(minMs, maxMs) &&
                    targetFinder.Search(NpcNameToFind, playerReader.Bits.TargetIsNotDead, sideActivityCts.Token))
                {
                    sideActivityCts.Cancel();
                    sideActivityManualReset.Reset();
                }

                sideActivityManualReset.WaitOne();
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("LookingForTarget thread stopped!");
        }

        private void Thread_AttendedGather()
        {
            sideActivityManualReset.WaitOne();

            while (!sideActivityCts.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - onEnterTime).TotalMilliseconds > MIN_TIME_TO_START_CYCLE_PROFESSION)
                {
                    AlternateGatherTypes();
                }
                sideActivityCts.Token.WaitHandle.WaitOne(CYCLE_PROFESSION_PERIOD);
                sideActivityManualReset.WaitOne();
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("AttendedGather thread stopped!");
        }

        private void AlternateGatherTypes()
        {
            var oldestKey = classConfig.GatherFindKeyConfig.MaxBy(x => x.MillisecondsSinceLastClick);
            if (!playerReader.IsCasting() &&
                oldestKey?.MillisecondsSinceLastClick > CYCLE_PROFESSION_PERIOD)
            {
                logger.LogInformation($"[{oldestKey.Key}] {oldestKey.Name} pressed for {input.defaultKeyPress}ms");
                input.Proc.KeyPress(oldestKey.ConsoleKey, input.defaultKeyPress);
                oldestKey.SetClicked();
            }
        }

        private void MountIfRequired()
        {
            if (shouldMount && classConfig.UseMount && !npcNameFinder.MobsVisible &&
                mountHandler.CanMount())
            {
                shouldMount = false;
                Log("Mount up");
                mountHandler.MountUp();
                navigation.ResetStuckParameters();
            }
        }

        #region Refill rules

        private void Navigation_OnPathCalculated()
        {
            MountIfRequired();
        }

        private void Navigation_OnDestinationReached()
        {
            if (debug)
                LogDebug("Navigation_OnDestinationReached");
            RefillWaypoints(false);
        }

        private void Navigation_OnWayPointReached()
        {
            if (classConfig.Mode == Mode.AttendedGather)
            {
                shouldMount = true;
            }

            MountIfRequired();
        }

        public void RefillWaypoints(bool onlyClosest)
        {
            Log($"RefillWaypoints - findClosest:{onlyClosest} - ThereAndBack:{input.ClassConfig.PathThereAndBack}");

            Vector3 playerMap = playerReader.MapPos;
            Vector3[] pathMap = mapRoute.ToArray();

            float mapDistanceToFirst = playerMap.MapDistanceXYTo(pathMap[0]);
            float mapDistanceToLast = playerMap.MapDistanceXYTo(pathMap[^1]);

            if (mapDistanceToLast < mapDistanceToFirst)
            {
                Array.Reverse(pathMap);
            }

            Vector3 mapClosestPoint = pathMap.OrderBy(p => playerMap.MapDistanceXYTo(p)).First();
            if (onlyClosest)
            {
                var closestPath = new Vector3[] { mapClosestPoint };

                if (debug)
                    LogDebug($"RefillWaypoints: Closest wayPoint: {mapClosestPoint}");
                navigation.SetWayPoints(closestPath);

                return;
            }

            int closestIndex = Array.IndexOf(pathMap, mapClosestPoint);
            if (mapClosestPoint == pathMap[0] || mapClosestPoint == pathMap[^1])
            {
                if (input.ClassConfig.PathThereAndBack)
                {
                    navigation.SetWayPoints(pathMap);
                }
                else
                {
                    Array.Reverse(pathMap);
                    navigation.SetWayPoints(pathMap);
                }
            }
            else
            {
                Vector3[] points = pathMap.Take(closestIndex).ToArray();
                Array.Reverse(points);
                Log($"RefillWaypoints - Set destination from closest to nearest endpoint - with {points.Length} waypoints");
                navigation.SetWayPoints(points);
            }
        }

        #endregion

        public void ReceivePath(Vector3[] newMapRoute)
        {
            mapRoute = newMapRoute;
        }

        private void RandomJump()
        {
            if ((DateTime.UtcNow - onEnterTime).TotalSeconds > 5 && classConfig.Jump.MillisecondsSinceLastClick > Random.Shared.Next(10_000, 25_000))
            {
                Log("Random jump");
                input.Jump();
            }
        }

        private void LogDebug(string text)
        {
            logger.LogDebug($"{nameof(FollowRouteGoal)}: {text}");
        }

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(FollowRouteGoal)}: {text}");
        }
    }
}