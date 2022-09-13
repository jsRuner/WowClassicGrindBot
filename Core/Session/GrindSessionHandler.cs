﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Core.Session
{
    public sealed class GrindSessionHandler : IGrindSessionHandler
    {
        private readonly ILogger logger;
        private readonly AddonReader addonReader;
        private readonly IGrindSessionDAO grindSessionDAO;
        private readonly CancellationToken ct;

        private readonly GrindSession session;
        private readonly Thread thread;

        private bool active;

        public GrindSessionHandler(ILogger logger, DataConfig dataConfig, AddonReader addonReader, IGrindSessionDAO grindSessionDAO, CancellationTokenSource cts)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.grindSessionDAO = grindSessionDAO;
            ct = cts.Token;

            session = new()
            {
                ExpList = ExperienceProvider.GetExperienceList(dataConfig)
            };

            thread = new Thread(PeriodicSave);
            thread.Start();
        }

        public void Start(string path)
        {
            active = true;

            session.SessionId = Guid.NewGuid();
            session.PathName = path;
            session.PlayerClass = addonReader.PlayerReader.Class;
            session.SessionStart = DateTime.Now;
            session.LevelFrom = addonReader.PlayerReader.Level.Value;
            session.XpFrom = addonReader.PlayerReader.PlayerXp.Value;
            session.MobsKilled = addonReader.LevelTracker.MobsKilled;
        }

        public void Stop(string reason, bool active)
        {
            this.active = active;

            session.SessionEnd = DateTime.Now;
            session.LevelTo = addonReader.PlayerReader.Level.Value;
            session.XpTo = addonReader.PlayerReader.PlayerXp.Value;
            session.Reason = reason;
            session.Death = addonReader.LevelTracker.Death;
            session.MobsKilled = addonReader.LevelTracker.MobsKilled;

            if (session.MobsKilled > 0 && session.TotalTimeInMinutes > 0)
                Save();
        }

        private void Save()
        {
            grindSessionDAO.Save(session);
        }

        private void PeriodicSave()
        {
            while (!ct.IsCancellationRequested)
            {
                if (active)
                    Stop("auto save", true);

                ct.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("SessionHandler thread stopped!");
        }
    }
}
