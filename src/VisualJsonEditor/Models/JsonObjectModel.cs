//-----------------------------------------------------------------------
// <copyright file="JsonObjectModel.cs" company="Visual JSON Editor">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>http://visualjsoneditor.codeplex.com/license</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NJsonSchema;
using NJsonSchema.Validation;

namespace VisualJsonEditor.Models
{
    /// <summary>Represents a JSON object. </summary>
    public class JsonObjectModel : JsonTokenModel
    {
        /// <summary>Creates a default <see cref="JsonObjectModel"/> from the given schema. </summary>
        /// <param name="schema">The <see cref="JsonSchema4"/>. </param>
        /// <returns>The <see cref="JsonObjectModel"/>. </returns>
        public static JsonObjectModel FromSchema(JsonSchema schema)
        {
            schema = schema.ActualSchema;

            JsonObjectModel obj = new JsonObjectModel();
            foreach (KeyValuePair<string, JsonSchemaProperty> property in schema.Properties)
            {
                JsonSchema propertySchema = property.Value.ActualSchema;
                if (propertySchema.Type.HasFlag(JsonObjectType.Object))
                {
                    if (property.Value.IsRequired)
                    {
                        obj[property.Key] = FromSchema(propertySchema);
                    }
                    else
                    {
                        obj[property.Key] = null;
                    }
                }
                else if (propertySchema.Type.HasFlag(JsonObjectType.Array))
                {
                    if (property.Value.IsRequired)
                    {
                        obj[property.Key] = new ObservableCollection<JsonTokenModel>();
                    }
                    else
                    {
                        obj[property.Key] = null;
                    }
                }
                else
                {
                    obj[property.Key] = GetDefaultValue(property);
                }
            }
            obj.Schema = schema;
            return obj;
        }

        /// <summary>Creates a <see cref="JsonObjectModel"/> from the given JSON data and <see cref="JsonSchema4"/>. </summary>
        /// <param name="jsonData">The JSON data. </param>
        /// <param name="schema">The <see cref="JsonSchema4"/>. </param>
        /// <returns>The <see cref="JsonObjectModel"/>. </returns>
        public static JsonObjectModel FromJson(string jsonData, JsonSchema schema)
        {
            object json = JsonConvert.DeserializeObject(jsonData);
            return FromJson((JObject)json, schema);
        }

        /// <summary> Checks the JSON data for a property called "_schema" </summary>
        /// <param name="filePath">The JSON document file path. </param>
        /// <returns>The path to the schema file if one is defined as a document property. </returns>
        public static string GetSchemaProperty(string filePath)
        {
            JToken schemaToken;

            JObject json = (JObject)JsonConvert.DeserializeObject(File.ReadAllText(filePath, Encoding.UTF8));

            if (!json.TryGetValue("$schema", out schemaToken))
            {
                return null;
            }

            string relativeSchemaPath = ((JValue)schemaToken).Value as string;
            relativeSchemaPath = relativeSchemaPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Path.GetDirectoryName(filePath), relativeSchemaPath);
        }

        /// <summary>Creates a <see cref="JsonObjectModel"/> from the given <see cref="JObject"/> and <see cref="JsonSchema4"/>. </summary>
        /// <param name="obj">The <see cref="JObject"/>. </param>
        /// <param name="schema">The <see cref="JsonSchema4"/>. </param>
        /// <returns>The <see cref="JsonObjectModel"/>. </returns>
        public static JsonObjectModel FromJson(JObject obj, JsonSchema schema)
        {
            schema = schema.ActualSchema;

            JsonObjectModel result = new JsonObjectModel();
            foreach (KeyValuePair<string, JsonSchemaProperty> property in schema.Properties)
            {
                JsonSchema propertySchema = property.Value.ActualSchema;
                if (propertySchema.Type.HasFlag(JsonObjectType.Array))
                {
                    JsonSchema propertyItemSchema = propertySchema.Item != null ? propertySchema.Item.ActualSchema : null;
                    if (obj[property.Key] != null)
                    {
                        IEnumerable<JsonTokenModel> objects = obj[property.Key].Select(o => o is JObject ?
                            (JsonTokenModel)FromJson((JObject)o, propertyItemSchema) :
                            JsonValueModel.FromJson((JValue)o, propertyItemSchema));

                        ObservableCollection<JsonTokenModel> list = new ObservableCollection<JsonTokenModel>(objects);
                        foreach (JsonTokenModel item in list)
                        {
                            item.ParentList = list;
                        }

                        result[property.Key] = list;
                    }
                    else
                    {
                        result[property.Key] = null;
                    }
                }
                else if (propertySchema.Type.HasFlag(JsonObjectType.Object) || propertySchema.Type == JsonObjectType.None)
                {
                    JToken token = obj[property.Key];
                    if (token is JObject)
                    {
                        result[property.Key] = FromJson((JObject)token, propertySchema);
                    }
                    else
                    {
                        result[property.Key] = null;
                    }
                }
                else
                {
                    JToken value;
                    if (obj.TryGetValue(property.Key, out value))
                    {
                        result[property.Key] = ((JValue)value).Value;
                    }
                    else
                    {
                        result[property.Key] = GetDefaultValue(property);
                    }
                }
            }
            result.Schema = schema;
            return result;
        }

        private static object GetDefaultValue(KeyValuePair<string, JsonSchemaProperty> property)
        {
            JsonSchema propertySchema = property.Value.ActualSchema;
            if (propertySchema.Default != null)
            {
                return propertySchema.Default;
            }

            if (propertySchema.Type.HasFlag(JsonObjectType.Boolean))
            {
                return false;
            }

            if (propertySchema.Type.HasFlag(JsonObjectType.String) && propertySchema.Format == JsonFormatStrings.DateTime)
            {
                return new DateTime();
            }

            if (propertySchema.Type.HasFlag(JsonObjectType.String) && propertySchema.Format == "date") // TODO: What to do with date/time?
            {
                return new DateTime();
            }

            if (propertySchema.Type.HasFlag(JsonObjectType.String) && propertySchema.Format == "time")
            {
                return new TimeSpan();
            }

            if (propertySchema.Type.HasFlag(JsonObjectType.String))
            {
                return string.Empty;
            }

            return null;
        }

        /// <summary>Gets the object's properties. </summary>
        public IEnumerable<JsonPropertyModel> Properties
        {
            get
            {
                List<JsonPropertyModel> properties = new List<JsonPropertyModel>();
                if (Schema.Properties != null)
                {
                    foreach (KeyValuePair<string, JsonSchemaProperty> propertyInfo in Schema.Properties)
                    {
                        JsonPropertyModel property = new JsonPropertyModel(propertyInfo.Key, this, propertyInfo.Value);
                        if (property.Value is ObservableCollection<JsonTokenModel>)
                        {
                            foreach (JsonTokenModel obj in (ObservableCollection<JsonTokenModel>)property.Value)
                            {
                                obj.Schema = propertyInfo.Value.Item != null ? propertyInfo.Value.Item.ActualSchema : null;
                            }
                        }
                        properties.Add(property);
                    }
                }
                return properties;
            }
        }

        /// <summary>Converts the <see cref="JsonTokenModel"/> to a <see cref="JToken"/>. </summary>
        /// <returns>The <see cref="JToken"/>. </returns>
        public override JToken ToJToken()
        {
            JObject obj = new JObject();
            foreach (KeyValuePair<string, object> pair in this)
            {
                if (pair.Value is ObservableCollection<JsonTokenModel>)
                {
                    ObservableCollection<JsonTokenModel> array = (ObservableCollection<JsonTokenModel>)pair.Value;
                    JArray jArray = new JArray();
                    foreach (JsonTokenModel item in array)
                    {
                        jArray.Add(item.ToJToken());
                    }

                    obj[pair.Key] = jArray;
                }
                else if (pair.Value is JsonTokenModel)
                {
                    obj[pair.Key] = ((JsonTokenModel)pair.Value).ToJToken();
                }
                else if (pair.Value != null || Schema.Properties[pair.Key].IsRequired)
                {
                    obj[pair.Key] = new JValue(pair.Value);
                }
            }
            return obj;
        }

        /// <summary>Validates the data of the <see cref="JsonObjectModel"/>. </summary>
        /// <returns>The list of validation errors. </returns>
        public Task<string> ValidateAsync()
        {
            return Task.Run(() =>
            {
                string json = ToJson();
                ICollection<ValidationError> errors = Schema.Validate(json);
                return string.Join("\n", ConvertErrors(errors, string.Empty));
            });
        }

        private static string[] ConvertErrors(IEnumerable<ValidationError> errors, string padding)
        {
            return errors.Select(error =>
            {
                StringBuilder output = new StringBuilder(string.Format("{0}{1}: {2}", padding, error.Path, error.Kind));
                if (error is ChildSchemaValidationError)
                {
                    foreach (KeyValuePair<JsonSchema, ICollection<ValidationError>> childError in ((ChildSchemaValidationError)error).Errors)
                    {
                        string schemaTitle = !string.IsNullOrEmpty(childError.Key.Title) ? childError.Key.Title : "Schema";
                        output.Append(string.Format("\n{0}  {1}:", padding, schemaTitle));
                        foreach (string x in ConvertErrors(childError.Value, padding + "    "))
                        {
                            output.Append(string.Format("\n{0}", x));
                        }
                    }
                }
                return output.ToString();
            }).ToArray();
        }
    }
}