﻿using System;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Linq;
using Anmat.Server.Core.Data;
using Anmat.Server.Core.Model;
using Anmat.Server.Core.Properties;
using System.Collections.Generic;

namespace Anmat.Server.Core
{
	public class SQLiteGenerator : ISQLGenerator
    {
		private readonly StringBuilder scriptBuilder;
		private readonly IRepository<UpdateVersion> versionRepository;
		private readonly AnmatConfiguration configuration;

		public SQLiteGenerator (IRepository<UpdateVersion> versionRepository, AnmatConfiguration configuration)
		{
			this.scriptBuilder = new StringBuilder ();
			this.versionRepository = versionRepository;
			this.configuration = configuration;
		}

		public string FileExtension { get { return ".sqlite"; } }

        public string Script { get; private set; }

		/// <exception cref="SQLGenerationException">SQLGenerationException</exception>
		public string GenerateDatabase (IEnumerable<IDocumentGenerator> documentGenerators)
		{
			return this.GenerateDatabase (documentGenerators.ToArray ());
		}

		/// <exception cref="SQLGenerationException">SQLGenerationException</exception>
        public string GenerateDatabase(params IDocumentGenerator[] documentGenerators)
        {
			this.scriptBuilder.Clear ();

			var version = this.versionRepository.GetAll ().Max (v => v.Number);
			var databaseFileName = Path.Combine (this.configuration.GetVersionPath (version), this.configuration.TargetDatabaseName + this.FileExtension);

			if (!Directory.Exists (Path.GetDirectoryName(databaseFileName))) {
				Directory.CreateDirectory (Path.GetDirectoryName(databaseFileName));
			}

			if(!configuration.ReplaceExistingTargetDatabase && File.Exists(databaseFileName)) {
				throw new SQLGenerationException (string.Format(Resources.SQLiteGenerator_DatabaseAlreadyExists, databaseFileName));
			}

			SQLiteConnection.CreateFile (databaseFileName);

			var connectionString = string.Format("Data Source={0};Version=3;", databaseFileName);

			using (var connection = new SQLiteConnection (connectionString)) {
				connection.Open ();

				this.CreateDocumentTables (documentGenerators, connection);
				this.CreateVersionTable (version, connection);

				connection.Close ();
			}

			this.Script = scriptBuilder.ToString ();

			return databaseFileName;
        }

		private void CreateDocumentTables(IDocumentGenerator[] documentGenerators, SQLiteConnection connection)
		{
			foreach (var documentGenerator in documentGenerators) {
				var document = documentGenerator.Generate ();
				var tableScript = this.GetCreateTableScript(documentGenerator.Metadata);
				var tableCommand = new SQLiteCommand(tableScript, connection);

				tableCommand.ExecuteNonQuery();
				this.scriptBuilder.AppendLine (tableScript);

				foreach(var row in document.Rows) {
					var insertScript = this.GetInsertScript(documentGenerator.Metadata, row);
					var insertCommand = new SQLiteCommand(insertScript, connection);

					insertCommand.ExecuteNonQuery();
					this.scriptBuilder.AppendLine(insertScript);
				}
			}
		}

		private void CreateVersionTable(int version, SQLiteConnection connection)
		{
			var updateVersion = this.versionRepository.Get (v => v.Number == version);
			
			var tableScript = "CREATE TABLE version (numero	INT NOT NULL, ultima_actualizacion TEXT NOT NULL)";
			var tableCommand = new SQLiteCommand(tableScript, connection);

			var insertScript = string.Format ("INSERT INTO version (numero, ultima_actualizacion) VALUES ({0}, '{1}')", updateVersion.Number, updateVersion.Date.ToString ());
			var insertCommand = new SQLiteCommand(insertScript, connection);

			tableCommand.ExecuteNonQuery();
			insertCommand.ExecuteNonQuery();

			this.scriptBuilder.AppendLine(insertScript);
			this.scriptBuilder.AppendLine (tableScript);
		}

		private string GetCreateTableScript(DocumentMetadata metadata)
		{
			var scriptBuilder = new StringBuilder ();

			scriptBuilder.Append ("CREATE TABLE ");
			scriptBuilder.Append (metadata.DocumentName);
			scriptBuilder.Append ("(");

			var i = 1;

			foreach (var columnMetadaData in metadata.Columns)
			{
				 scriptBuilder.Append (string.Format ("{0} {1}", columnMetadaData.Name, this.GetSQLiteType (columnMetadaData)));

				 if (i < metadata.Columns.Count ()) {
					scriptBuilder.Append (", ");
				 }

				i++;
			}

			scriptBuilder.Append(");");

			return scriptBuilder.ToString ();
		}

		private string GetInsertScript (DocumentMetadata metadata, Row row)
		{
			var scriptBuilder = new StringBuilder ();

			scriptBuilder.Append ("INSERT INTO ");
			scriptBuilder.Append (metadata.DocumentName);
			scriptBuilder.Append ("(");

			var columnNames = metadata.Columns.Select (c => c.Name);
			var columns = string.Join (", ", columnNames);

			scriptBuilder.Append (columns);
			scriptBuilder.Append (") VALUES (");

			for (var i = 0; i < row.Cells.Count (); i++) {
				var cell = row.Cells.ElementAt (i);

				if(cell.Type == typeof(string) || cell.Type == typeof(DateTime)) {
					scriptBuilder.Append (string.Format("'{0}'", cell.Value));
				} else if(cell.Type == typeof(bool))
				{
					scriptBuilder.Append (string.Format ("{0}", cell.Value.ToLower () == "true" ? 1 : 0));
				} else
				{
					scriptBuilder.Append (string.IsNullOrEmpty(cell.Value) ? "NULL" : cell.Value);	
				}

				if (i == row.Cells.Count () - 1) {
					scriptBuilder.Append (");");
				} else {
					scriptBuilder.Append (", ");
				}
			}

			return scriptBuilder.ToString ();
		}

		private string GetSQLiteType(DocumentColumnMetadata columnMetadata)
		{
			var columnType = columnMetadata.GetType();
			var sqlType = columnType == typeof (double) ? "REAL" :
				columnType == typeof (int) ? "INT" :
						columnType == typeof (bool) ? "INT" :
				"TEXT";

			return columnMetadata.IsNullable ? sqlType : string.Format ("{0} NOT NULL", sqlType);
		}
	}
}
