Instance: questionnaire-definition
InstanceOf: SearchParameter
Usage: #definition
* url = "http://hl7.org/fhir/uv/sdc/SearchParameter/questionnaire-definition"
* name = "SDCQuestionnaireItemDefinition"
* status = #active
* date = "2016-03-31T08:01:25+11:00"
* publisher = "Health Level Seven"
* description = "Allows searching by 'definition' element within a Questionnaire's items"
* code = #definition
* base = #Questionnaire
* type = #token
* expression = "Questionnaire.item.definition"
* xpath = "f:Questionnaire/f:item/f:definition"
* xpathUsage = #normal
