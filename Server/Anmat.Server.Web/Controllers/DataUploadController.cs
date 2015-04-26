﻿using Anmat.Server.Web.Filters;
using System;
using System.IO;
using System.Web.Mvc;
using Anmat.Server.Core;
using Anmat.Server.Core.Context;
using System.Linq;
using Anmat.Server.Core.Data;
using System.Threading.Tasks;

namespace Anmat.Server.Web.Controllers
{
    [Authorize]
    [InitializeSimpleMembership]
    public class DataUploadController : Controller
    {
		private readonly IAnmatContext context;
		private readonly IAnmatGenerationServiceClient anmatDataGenerationServiceClient;

		public DataUploadController ()
		{
			this.context = AnmatContext.Initialize ();
			this.anmatDataGenerationServiceClient = new AnmatGenerationServiceClient (this.context.Configuration);
		}

		[HttpGet]
        public ActionResult Index()
        {
			this.ViewBag.Version = this.context.VersionService.GetLatestVersion ();

            return View();
        }
		
		[HttpGet]
		public ActionResult Jobs()
		{
			var jobs = this.context.JobService.GetJobs ().OrderByDescending(j => j.Version);

			return View(jobs.ToList());
		}

		[HttpPost]
        public ActionResult StartJob()
        {
			var version = this.GetNewVersion ();
			
			var tempPath = AnmatConfiguration.GetTempVersionPath (version);
			var tempFiles = Directory.EnumerateFiles (tempPath);

			var invalidFiles = tempFiles.Select(f => Path.GetFileNameWithoutExtension(f)).Where (f => f != this.context.Configuration.TargetMedicinesTableName &&
				f != this.context.Configuration.TargetActiveComponentsTableName);

			if (invalidFiles.Any()) {
				var message = string.Format ("Uno o mas archivos tienen nombre incorrecto.\nNombres incorrectos: {0}.\nNombres esperados: {1}", string.Join (", ", invalidFiles), string.Concat (this.context.Configuration.TargetMedicinesTableName, ", ", this.context.Configuration.TargetActiveComponentsTableName));
				
				return Json(new { success = false, message = message });
			}

			var latestJob = this.context.JobService.GetJobs ().FirstOrDefault (j => j.Version == version);

			if (latestJob != null && latestJob.Status == DataGenerationJobStatus.InProgress) {
				return Json(new { success = false, message = "Existe un job en proceso. No se puede iniciar otro hasta que el actual finalice" });
			}

			var newJob = this.context.JobService.CreateJob (version);

			try {
				Task.Run (() => {
					this.anmatDataGenerationServiceClient.ProcessJob (newJob.Id);
				});
			} catch (Exception ex) {
				var message = string.Format ("Ha ocurrido un error al procesar el job {0}. Detalles: {1}", newJob.Id, ex.Message);

				return Json(new { success = false, message = message });
			}

            return Json(new { success = true });
        }

        public ActionResult SaveUploadedFile()
        {
            try
            {
                foreach (string requestFileName in Request.Files)
                {
                    var file = Request.Files[requestFileName];

                    if (file != null && file.ContentLength > 0)
                    {
						var tempPath = this.GetNewVersionTempPath ();
                        var fileName = Path.GetFileName(file.FileName);
						var tempFilePath = Path.Combine (tempPath, fileName);

                        file.SaveAs(tempFilePath);
                    }
                }

				 return Json(new { success = true });

            }
            catch (Exception)
            {
                return Json(new { success = false });
            }
        }

		private string GetNewVersionTempPath()
		{
			var version = this.GetNewVersion ();

			return AnmatConfiguration.GetTempVersionPath (version);
		}

		private string GetNewVersionPath()
		{
			var version = this.GetNewVersion ();

			return this.context.Configuration.GetVersionPath (version);
		}

		private int GetNewVersion()
		{
			return this.context.VersionService.GetLatestVersion ().Number + 1;
		}
    }
}