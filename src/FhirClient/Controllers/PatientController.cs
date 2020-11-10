using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using FhirClient.Models;
using FhirClient.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using System.Xml.Linq;
using Hl7.Fhir.Serialization;

namespace FhirClient.Controllers
{
    public class PatientController : Controller
    {
        private IEasyAuthProxy _easyAuthProxy { get; set; }
        private IConfiguration Configuration { get; set; }

        public PatientController(IEasyAuthProxy easyAuthProxy, IConfiguration config)
        {
            _easyAuthProxy = easyAuthProxy;
            Configuration = config;
        }

		public IActionResult Index()
		{
			var client = GetClient();
			List<Patient> patientResults = new List<Patient>();

			try
			{
				Bundle result;
				if (!String.IsNullOrEmpty(Request.Query["ct"]))
				{
					string cont = Request.Query["ct"];
					result = client.Search<Patient>(new string[] { $"ct={cont}" });
				}
				else
				{
					var q = new SearchParams()
						.Where("name=ParamTest")
						.LimitTo(100);
					//q.Add("birthdate", "lt2014-01-01");
					result = client.Search<Patient>(q);
				}

				if (result.Entry != null)
				{
					foreach (var e in result.Entry)
					{
						patientResults.Add((Patient)e.Resource);
					}
				}

				if (result.NextLink != null)
				{
					ViewData["NextLink"] = result.NextLink.PathAndQuery;
				}

			}
			catch (Exception e)
			{
				ViewData["ErrorMessage"] = e.Message;
			}

			return View(patientResults);
		}

		[HttpGet("/Patient/{id}")]
		public IActionResult Details(string id)
		{
			var client = GetClient();
			PatientRecord patientRecord = new PatientRecord();

			try
			{
				var patientResult = client.Search<Patient>(new string[] { $"_id={id}" });
				if ((patientResult.Entry != null) && (patientResult.Entry.Count > 0))
				{
					patientRecord.Patient = (Patient)(patientResult.Entry[0].Resource);
				}

				if (patientRecord.Patient != null)
				{
					patientRecord.Observations = new List<Observation>();
					var observationResult = client.Search<Observation>(new string[] { $"subject=Patient/{patientRecord.Patient.Id}" });

					while (observationResult != null)
					{
						foreach (var o in observationResult.Entry)
						{
							patientRecord.Observations.Add((Observation)o.Resource);
						}
						observationResult = client.Continue(observationResult);
					}

					patientRecord.Encounters = new List<Encounter>();
					var encounterResult = client.Search<Encounter>(new string[] { $"subject=Patient/{patientRecord.Patient.Id}" });

					while (encounterResult != null)
					{
						foreach (var e in encounterResult.Entry)
						{
							patientRecord.Encounters.Add((Encounter)e.Resource);
						}
						encounterResult = client.Continue(encounterResult);
					}

				}
			}
			catch (Exception e)
			{
				ViewData["ErrorMessage"] = e.Message;
			}

			return View(patientRecord);
		}


		[HttpPost]
		public IActionResult Create(PatientInfo model)
		{
			string message;

			if (ModelState.IsValid)
			{
				var human = new HumanName
				{
					Use = HumanName.NameUse.Official,
					Prefix = new string[] { "Mr" },
					Given = new string[] { model.Name },
					Family = model.Surname
				};

				var patientIdentifier = new Identifier
				{
					System = "http://ns.electronichealth.net.au/id/hi/ihi/1.0",
					Value = "8003608166690503"
				};

				var patient = new Patient
				{
					Name = new List<HumanName>
					{
						human
					},
					Identifier = new List<Identifier>
					{
						patientIdentifier
					}
				};

				var client = GetClient();
				var createdPatient = client.Create(patient);

				var fhirXmlSerializer = new FhirXmlSerializer();
				var xml = fhirXmlSerializer.SerializeToString(createdPatient, SummaryType.False);
				var xDoc = XDocument.Parse(xml);

				message = "Name: " + model.Name + ", with  Family Name: " + model.Surname.ToString() + " was created successfully" + "\n\n\n" + xDoc.ToString();
			}
			else
			{
				message = "Failed to create the product. Please try again";
			}

			return Content(message);
		}

		private Hl7.Fhir.Rest.FhirClient GetClient()
		{
			var client = new Hl7.Fhir.Rest.FhirClient(Configuration["FhirServerUrl"]);
			string token = null;// await _easyAuthProxy.GetAadAccessToken();
			client.OnBeforeRequest += (object sender, BeforeRequestEventArgs e) =>
			{
				e.RawRequest.Headers.Add("Authorization", $"Bearer {token}");
			};
			client.PreferredFormat = ResourceFormat.Json;
			return client;
		}
	}
}