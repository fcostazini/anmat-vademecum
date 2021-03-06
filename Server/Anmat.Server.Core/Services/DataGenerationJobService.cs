﻿using System;
using System.Collections.Generic;
using Anmat.Server.Core.Data;
using Anmat.Server.Core.Properties;
using System.Linq;

namespace Anmat.Server.Core.Services
{
	public class DataGenerationJobService : IDataGenerationJobService
	{
		private readonly IRepository<DataGenerationJob> repository;

		public DataGenerationJobService (IRepository<DataGenerationJob> repository)
		{
			this.repository = repository;
		}

		public DataGenerationJob CreateJob (int version)
		{
			var job = new DataGenerationJob {
				Status = DataGenerationJobStatus.InProgress,
				DateStarted = DateTime.Now.ToString(),
				Version = version
			};

			this.repository.Create (job);

			return job;
		}

		public void UpdateJob (DataGenerationJob job)
		{
			var existingJob = this.repository.Get (j => j.Id == job.Id);

			existingJob.Status = job.Status;
			existingJob.DateFinished = job.DateFinished;
			existingJob.Message = job.Message;

			this.repository.Update (existingJob);
		}

		public void FinishJob (DataGenerationJob job)
		{
			var existingJob = this.repository.Get (j => j.Id == job.Id);

			existingJob.Status = DataGenerationJobStatus.Completed;
			existingJob.DateFinished = DateTime.Now.ToString();
			existingJob.Message = string.Format (Resources.DataGenerationJobService_JobCompleted, job.Id, job.Version);

			this.repository.Update (existingJob);
		}

		public IEnumerable<DataGenerationJob> GetJobs ()
		{
			return this.repository.GetAll ();
		}

		public DataGenerationJob GetLatestJob ()
		{
			var jobs = this.repository.GetAll ();

			if (!jobs.Any ()) {
				return null;
			}

			var latestJobVersion = jobs.Max (j => j.Version);

			return this.repository.Get(j => j.Version == latestJobVersion);
		}
	}
}
