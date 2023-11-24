//-----------------------------------------------------------------------
// <copyright file="JsonPropertyModel.cs" company="Visual JSON Editor">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>http://visualjsoneditor.codeplex.com/license</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Text;

using MyToolkit.Model;

using NJsonSchema;

namespace VisualJsonEditor.Models
{
    /// <summary>Describes a JSON property. </summary>
    public class JsonPropertyModel : ObservableObject
    {
        /// <summary>Initializes a new instance of the <see cref="JsonPropertyModel"/> class. </summary>
        /// <param name="name">The name of the property. </param>
        /// <param name="parent">The parent object. </param>
        /// <param name="schema">The property type as schema object. </param>
        public JsonPropertyModel(string name, JsonObjectModel parent, JsonSchemaProperty schema)
        {
            Name = name;
            Parent = parent;
            Schema = schema;

            Parent.PropertyChanged += (sender, args) => RaisePropertyChanged(() => Value);
        }

        /// <summary>Gets the property name. </summary>
        public string Name { get; private set; }

        /// <summary>Gets the parent object. </summary>
        public JsonObjectModel Parent { get; private set; }

        /// <summary>Gets the property type as schema. </summary>
        public JsonSchemaProperty Schema { get; private set; }

        /// <summary>Gets a value indicating whether the property is required. </summary>
        public bool IsRequired => Schema.IsRequired;

        /// <summary>Gets the contentEncoding value. </summary>
        public string GetContentEncoding
        {
            get
            {
                object value = null;
                Schema.ExtensionData?.TryGetValue("contentEncoding", out value);
                return value?.ToString();
            }
        }

        /// <summary>Indicates if this value is base64 encoded. </summary>
        public bool IsBase64String => Schema.Type == JsonObjectType.String &&
                    (GetContentEncoding == "base64"
                     || Schema.Format == JsonFormatStrings.Byte);

        /// <summary>Gets or sets the value of the property. </summary>
        public object Value
        {
            get
            {
                if (Parent.ContainsKey(Name))
                {
                    return IsBase64String
                        ? Base64Decode(Parent[Name].ToString())
                        : Parent[Name];
                }
                else
                {
                    return null;
                }
            }
            set
            {
                Parent[Name] = IsBase64String
                    ? Base64Encode(value.ToString())
                    : value;

                RaisePropertyChanged(() => Value);
                RaisePropertyChanged(() => HasValue);
            }
        }

        /// <summary>Gets a value indicating whether the property has a value. </summary>
        public bool HasValue => Value != null;

        /// <summary>Encode a string in Base64. </summary>
        private static string Base64Encode(string plainText)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>Decode a string in Base64. </summary>
        private static string Base64Decode(string base64EncodedData)
        {
            try
            {
                byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
                return Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch (Exception)
            {
                // Hide any decode error
                return "";
            }
        }
    }
}