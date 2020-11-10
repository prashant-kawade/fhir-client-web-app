using System.Collections.Generic;
using Hl7.Fhir.Model;

namespace FhirClient.Models
{
    public class PatientRecord
    {
        public Patient Patient;
        public List<Observation> Observations { get; set; }
        public List<Encounter> Encounters { get; set; }
    }

}