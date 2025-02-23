// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System;
using System.Linq;
using System.Linq.Expressions;
using Moryx.AbstractionLayer;
using Moryx.AbstractionLayer.Products;
using Moryx.Container;
using Moryx.Model;
using Moryx.Modules;
using Moryx.Products.Model;
using Moryx.Serialization;
using Newtonsoft.Json;

namespace Moryx.Products.Management
{
    /// <summary>
    /// Strategy for simple products
    /// </summary>
    [ExpectedConfig(typeof(GenericTypeConfiguration))]
    [StrategyConfiguration(typeof(IProductType), DerivedTypes = true)]
    [Plugin(LifeCycle.Transient, typeof(IProductTypeStrategy), Name = nameof(GenericTypeStrategy))]
    internal class GenericTypeStrategy : TypeStrategyBase<GenericTypeConfiguration>, IProductTypeSearch
    {
        /// <summary>
        /// Injected entity mapper
        /// </summary>
        public GenericEntityMapper<ProductType, IProductPartLink> EntityMapper { get; set; }

        /// <summary>
        /// Initialize the type strategy
        /// </summary>
        public override void Initialize(ProductTypeConfiguration config)
        {
            base.Initialize(config);
            
            EntityMapper.Initialize(TargetType, Config);
        }

        public Expression<Func<IGenericColumns, bool>> TransformSelector<TProduct>(Expression<Func<TProduct, bool>> selector)
        {
            return EntityMapper.TransformSelector(selector);
        }

        public override bool HasChanged(IProductType current, IGenericColumns dbProperties)
        {
            return EntityMapper.HasChanged(dbProperties, current);
        }

        public override void SaveType(IProductType source, IGenericColumns target)
        {
            EntityMapper.WriteValue(source, target);
        }

        public override void LoadType(IGenericColumns source, IProductType target)
        {
            EntityMapper.ReadValue(source, target);
        }
    }
}
