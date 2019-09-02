﻿using System;
using System.Collections.Generic;
using Marvin.AbstractionLayer;

namespace Marvin.Products.Management
{
    /// <summary>
    /// Component to handle all recipe operations
    /// </summary>
    internal interface IRecipeManagement
    {
        /// <summary>
        /// Loads the recipe by the given identifier
        /// </summary>
        IProductRecipe Get(long recipeId);

        /// <summary>
        /// Will load all recipes by the given product
        /// </summary>
        IReadOnlyList<IProductRecipe> GetAllByProduct(IProduct product);

        /// <summary>
        /// Retrieves a recipe for the product
        /// </summary>
        IReadOnlyList<IProductRecipe> GetRecipes(IProduct product, RecipeClassification classifications);

        /// <summary>
        /// A recipe was changed, give users the chance to update their reference
        /// </summary>
        event EventHandler<IRecipe> RecipeChanged;

        /// <summary>
        /// Save recipe to DB
        /// </summary>
        long Save(IProductRecipe instance);

        /// <summary>
        /// Saves multiple recipes
        /// </summary>
        void Save(long productId, ICollection<IProductRecipe> recipes);
    }
}