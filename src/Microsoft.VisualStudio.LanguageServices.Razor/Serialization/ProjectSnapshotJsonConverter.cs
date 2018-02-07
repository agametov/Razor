// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Serialization
{
    internal class ProjectSnapshotJsonConverter : JsonConverter
    {
        public static readonly ProjectSnapshotJsonConverter Instance = new ProjectSnapshotJsonConverter();

        public override bool CanConvert(Type objectType)
        {
            return typeof(ProjectSnapshot).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var obj = JObject.Load(reader);
            var filePath = obj[nameof(ProjectSnapshot.FilePath)].Value<string>();
            var configuration = obj[nameof(ProjectSnapshot.Configuration)].ToObject<RazorConfiguration>(serializer);

            return new SerializedProjectSnapshot(filePath, configuration);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var snapshot = (ProjectSnapshot)value;

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(ProjectSnapshot.FilePath));
            writer.WriteValue(snapshot.FilePath);

            writer.WritePropertyName(nameof(ProjectSnapshot.Configuration));
            serializer.Serialize(writer, snapshot.Configuration);

            writer.WriteEndObject();
        }
    }
}
