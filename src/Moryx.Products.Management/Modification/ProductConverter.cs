// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moryx.AbstractionLayer.Products;
using Moryx.AbstractionLayer.Recipes;
using Moryx.Container;
using Moryx.Serialization;
using Moryx.Tools;
using Moryx.Workflows;

namespace Moryx.Products.Management.Modification
{
    [Component(LifeCycle.Singleton, typeof(IProductConverter))]
    internal class ProductConverter : IProductConverter
    {
        #region Dependency Injection

        public IProductManager ProductManager { get; set; }

        public IRecipeManagement RecipeManagement { get; set; }

        public IWorkplans WorkplanManagement { get; set; }

        #endregion

        #region Fields and Properties

        // Null object pattern for identity
        private static readonly ProductIdentity EmptyIdentity = new ProductIdentity(string.Empty, 0);

        private static readonly ICustomSerialization ProductSerialization = new PartialSerialization<ProductType>();
        private static readonly ICustomSerialization RecipeSerialization = new PartialSerialization<ProductionRecipe>();

        #endregion

        #region To Model

        public ProductModel ConvertProduct(IProductType productType, bool flat)
        {
            // Base object
            var identity = (ProductIdentity)productType.Identity ?? EmptyIdentity;
            var converted = new ProductModel
            {
                Id = productType.Id,
                Type = productType.GetType().Name,
                Name = productType.Name,
                State = productType.State,
                Identifier = identity.Identifier,
                Revision = identity.Revision
            };

            if (flat)
                return converted;

            // Properties
            var properties = productType.GetType().GetProperties();
            converted.Properties = EntryConvert.EncodeObject(productType, ProductSerialization);

            // Files
            converted.Files = (from property in properties
                               where property.PropertyType == typeof(ProductFile)
                               select (ProductFile)property.GetValue(productType)).ToArray();
            converted.FileModels = ConvertFiles(productType, properties);

            // Recipes
            var recipes = RecipeManagement.GetAllByProduct(productType);
            converted.Recipes = recipes.Select(ConvertRecipe).ToArray();

            // Parts
            ConvertParts(productType, properties, converted);

            return converted;
        }

        public ProductDefinitionModel ConvertProductType(Type productType)
        {
            return new()
            {
                Name = productType.Name,
                DisplayName = productType.GetDisplayName() ?? productType.Name,
                BaseDefinition = productType.BaseType?.Name,
                Properties = EntryConvert.EncodeClass(productType, ProductSerialization)
            };
        }

        private ProductFileModel[] ConvertFiles(IProductType productType, IEnumerable<PropertyInfo> properties)
        {
            var productFileProperties = properties.Where(p => p.PropertyType == typeof(ProductFile)).ToArray();
            var fileModels = new ProductFileModel[productFileProperties.Length];
            for (int i = 0; i < fileModels.Length; i++)
            {
                var value = (ProductFile)productFileProperties[i].GetValue(productType);
                fileModels[i] = new ProductFileModel()
                {
                    PropertyName = productFileProperties[i].Name,
                    FileName = value?.Name,
                    FileHash = value?.FileHash,
                    FilePath = value?.FilePath,
                    MimeType = value?.MimeType
                };
            }
            return fileModels;
        }

        private void ConvertParts(IProductType productType, IEnumerable<PropertyInfo> properties, ProductModel converted)
        {
            var connectors = new List<PartConnector>();
            foreach (var property in properties)
            {
                var displayName = property.GetDisplayName();

                if (typeof(IProductPartLink).IsAssignableFrom(property.PropertyType))
                {
                    var link = (IProductPartLink)property.GetValue(productType);
                    var partModel = ConvertPart(link);
                    var connector = new PartConnector
                    {
                        Name = property.Name,
                        DisplayName = displayName,
                        Type = FetchProductType(property.PropertyType),
                        Parts = partModel is null ? new PartModel[0] : new[] { partModel },
                        PropertyTemplates = EntryConvert.EncodeClass(property.PropertyType, ProductSerialization)
                    };
                    connectors.Add(connector);
                }
                else if (typeof(IEnumerable<IProductPartLink>).IsAssignableFrom(property.PropertyType))
                {
                    var links = (IEnumerable<IProductPartLink>)property.GetValue(productType);
                    var linkType = property.PropertyType.GetGenericArguments()[0];
                    var connector = new PartConnector
                    {
                        IsCollection = true,
                        Name = property.Name,
                        DisplayName = displayName,
                        Type = FetchProductType(linkType),
                        Parts = links?.Select(ConvertPart).ToArray(),
                        PropertyTemplates = EntryConvert.EncodeClass(linkType, ProductSerialization)
                    };
                    connectors.Add(connector);
                }
            }
            converted.Parts = connectors.ToArray();
        }

        private PartModel ConvertPart(IProductPartLink link)
        {
            // No link, no DTO!
            if (link is null || link.Product is null)
                return null;

            var part = new PartModel
            {
                Id = link.Id,
                Product = ConvertProduct(link.Product, true),
                Properties = EntryConvert.EncodeObject(link, ProductSerialization)
            };
            return part;
        }

        private static string FetchProductType(Type linkType)
        {
            var partLinkInterface = linkType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProductPartLink<>));
            var prodType = partLinkInterface.GetGenericArguments()[0];
            return prodType.Name;
        }

        public RecipeModel ConvertRecipe(IRecipe recipe)
        {
            // Transform to DTO and transmit
            var converted = new RecipeModel
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Type = recipe.GetType().Name,
                State = recipe.State,
                Revision = recipe.Revision,
                Properties = EntryConvert.EncodeObject(recipe, RecipeSerialization),
                IsClone = recipe.Classification.HasFlag(RecipeClassification.Clone)
            };

            switch (recipe.Classification & RecipeClassification.CloneFilter)
            {
                case RecipeClassification.Unset:
                    converted.Classification = RecipeClassificationModel.Unset;
                    break;
                case RecipeClassification.Default:
                    converted.Classification = RecipeClassificationModel.Default;
                    break;
                case RecipeClassification.Alternative:
                    converted.Classification = RecipeClassificationModel.Alternative;
                    break;
                case RecipeClassification.Intermediate:
                    converted.Classification = RecipeClassificationModel.Intermediate;
                    break;
                case RecipeClassification.Part:
                    converted.Classification = RecipeClassificationModel.Part;
                    break;
            }

            var wpRecipe = recipe as IWorkplanRecipe;
            if (wpRecipe?.Workplan != null)
            {
                converted.WorkplanId = wpRecipe.Workplan.Id;
                converted.WorkplanModel = ConvertWorkplan(wpRecipe.Workplan);
            }
                

            return converted;
        }

        public WorkplanModel ConvertWorkplan(IWorkplan workplan)
        {
            var workplanDto = new WorkplanModel
            {
                Id = workplan.Id,
                Name = workplan.Name,
                Version = workplan.Version,
                State = workplan.State
            };

            return workplanDto;
        }

        #endregion

        #region Convert back

        public IProductRecipe ConvertRecipeBack(RecipeModel recipe, IProductRecipe productRecipe, IProductType productType)
        {
            productRecipe.Name = recipe.Name;
            productRecipe.Revision = recipe.Revision;
            productRecipe.State = recipe.State;

            // Only load workplan if it changed
            var workplanRecipe = productRecipe as IWorkplanRecipe;
            if (workplanRecipe != null && workplanRecipe.Workplan?.Id != recipe.WorkplanId)
                workplanRecipe.Workplan = WorkplanManagement.LoadWorkplan(recipe.WorkplanId);

            if (productRecipe.Product == null)
            {
                productRecipe.Product = productType;
            }

            EntryConvert.UpdateInstance(productRecipe, recipe.Properties, RecipeSerialization);

            // Do not update a clones classification
            if (productRecipe.Classification.HasFlag(RecipeClassification.Clone))
                return productRecipe;

            switch (recipe.Classification)
            {
                case RecipeClassificationModel.Unset:
                    productRecipe.Classification = RecipeClassification.Unset;
                    break;
                case RecipeClassificationModel.Default:
                    productRecipe.Classification = RecipeClassification.Default;
                    break;
                case RecipeClassificationModel.Alternative:
                    productRecipe.Classification = RecipeClassification.Alternative;
                    break;
                case RecipeClassificationModel.Intermediate:
                    productRecipe.Classification = RecipeClassification.Intermediate;
                    break;
                case RecipeClassificationModel.Part:
                    productRecipe.Classification = RecipeClassification.Part;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return productRecipe;
        }

        public IProductType ConvertProductBack(ProductModel source, ProductType converted)
        {
            // Copy base values
            converted.Identity = new ProductIdentity(source.Identifier, source.Revision);
            converted.Name = source.Name;
            converted.State = source.State;
            
            // Save recipes
            var recipes = new List<IProductRecipe>(source.Recipes?.Length ?? 0);
            foreach (var recipeModel in source.Recipes ?? Enumerable.Empty<RecipeModel>())
            {
                IProductRecipe productRecipe;
                if (recipeModel.Id == 0)
                {
                    var type = ReflectionTool.GetPublicClasses<IProductRecipe>(t => t.Name == recipeModel.Type).First();
                    productRecipe = (IProductRecipe)Activator.CreateInstance(type);
                }
                else
                    productRecipe = RecipeManagement.Get(recipeModel.Id);

                ConvertRecipeBack(recipeModel, productRecipe, converted);
                recipes.Add(productRecipe);
            }
            if (recipes.Any())
                RecipeManagement.Save(source.Id, recipes);

            // Product is flat
            if (source.Properties is null)
                return converted;

            // Copy extended properties
            var properties = converted.GetType().GetProperties();
            EntryConvert.UpdateInstance(converted, source.Properties, ProductSerialization);

            // Copy Files
            ConvertFilesBack(converted, source, properties);

            // Convert parts
            foreach (var partConnector in source.Parts ?? Enumerable.Empty<PartConnector>())
            {
                if (partConnector.Parts is null)
                    continue;

                var prop = properties.First(p => p.Name == partConnector.Name);
                var value = prop.GetValue(converted);
                if (partConnector.IsCollection)
                {
                    if (value == null)
                    {
                        value = Activator.CreateInstance(typeof(List<>)
                            .MakeGenericType(prop.PropertyType.GetGenericArguments().First()));
                        prop.SetValue(converted, value);
                    }
                    UpdateCollection((IList)value, partConnector.Parts);
                }
                else if (partConnector.Parts.Length == 1)
                {
                    if (value == null)
                    {
                        value = Activator.CreateInstance(prop.PropertyType);
                        prop.SetValue(converted, value);
                    }
                    UpdateReference((IProductPartLink)value, partConnector.Parts[0]);
                }
                else if (partConnector.Parts.Length == 0)
                {
                    prop.SetValue(converted, null);
                }
            }

            return converted;
        }

        private void UpdateCollection(IList value, IEnumerable<PartModel> parts)
        {
            // Track which part links are still represented by the models
            var unused = new List<IProductPartLink>(value.OfType<IProductPartLink>());
            // Iterate over the part models
            // Create or update the part links
            var elemType = value.GetType().GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
                .Select(i => i.GetGenericArguments()[0]).Single();
            foreach (var partModel in parts)
            {
                if (partModel is null)
                    continue;

                var match = unused.Find(r => r.Id == partModel?.Id);
                if (match == null)
                {
                    match = (IProductPartLink)Activator.CreateInstance(elemType);
                    value.Add(match);
                }
                else
                    unused.Remove(match);

                EntryConvert.UpdateInstance(match, partModel.Properties);
                match.Product = ProductManager.LoadType(partModel.Product.Id);
            }

            // Clear all values no longer present in the model
            foreach (var link in unused)
                value.Remove(link);
        }

        private void UpdateReference(IProductPartLink value, PartModel part)
        {
            EntryConvert.UpdateInstance(value, part.Properties);
            value.Product = part.Product is null ? null : ProductManager.LoadType(part.Product.Id);
        }

        private static void ConvertFilesBack(object converted, ProductModel product, PropertyInfo[] properties)
        {
            foreach (var fileModel in product.FileModels)
            {
                var prop = properties.Single(p => p.Name == fileModel.PropertyName);
                var productFile = new ProductFile()
                {
                    MimeType = fileModel.MimeType,
                    FilePath = fileModel.FilePath,
                    FileHash = fileModel.FileHash,
                    Name = fileModel.FileName
                };
                if (productFile.GetType().GetProperties().All(p => p.GetValue(productFile) is null))
                    prop.SetValue(converted, null);
                else
                    prop.SetValue(converted, productFile);
            }
        }
        #endregion
    }
}
