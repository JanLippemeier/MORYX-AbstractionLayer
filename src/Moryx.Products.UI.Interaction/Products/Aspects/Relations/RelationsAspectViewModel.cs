// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Moryx.ClientFramework.Tasks;
using Moryx.Products.UI.Interaction.Properties;
using Moryx.Products.UI.ProductService;

namespace Moryx.Products.UI.Interaction.Aspects
{
    [ProductAspectRegistration(nameof(RelationsAspectViewModel))]
    internal class RelationsAspectViewModel : ProductAspectViewModelBase
    {
        #region Dependencies

        public IProductServiceModel ProductServiceModel { get; set; }

        #endregion

        public override string DisplayName => Strings.RelationsAspectViewModel_DisplayName;

        /// <summary>
        /// Loaded parent relations of this product
        /// </summary>
        public ProductInfoViewModel[] Parents { get; private set; }

        private TaskNotifier _taskNotifier;

        /// <summary>
        /// Task notifier to display a busy indicator
        /// </summary>
        public TaskNotifier TaskNotifier
        {
            get => _taskNotifier;
            set
            {
                _taskNotifier = value;
                NotifyOfPropertyChange();
            }
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializeAsync(cancellationToken);

            var loaderTask = Task.Run(async delegate
            {
                var parents = await ProductServiceModel.GetProducts(new ProductQuery
                {
                    Identifier = Product.Identifier,
                    Revision = Product.Revision,
                    RevisionFilter = RevisionFilter.Specific,
                    Selector = Selector.Parent
                }).ConfigureAwait(false);

                Parents = parents.Select(p => new ProductInfoViewModel(p)).ToArray();
                Execute.OnUIThread(() => NotifyOfPropertyChange(nameof(Parents)));
            }, cancellationToken);

            TaskNotifier = new TaskNotifier(loaderTask);
        }
    }
}
