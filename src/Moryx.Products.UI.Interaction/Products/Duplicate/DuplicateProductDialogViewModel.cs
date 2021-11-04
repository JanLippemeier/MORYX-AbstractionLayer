// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Moryx.ClientFramework.Commands;
using Moryx.ClientFramework.Dialog;
using Moryx.ClientFramework.Tasks;
using Moryx.Products.UI.Interaction.Properties;
using Moryx.Products.UI.ProductService;

namespace Moryx.Products.UI.Interaction
{
    internal class DuplicateProductDialogViewModel : DialogScreen
    {
        #region Fields and Properties

        private readonly IProductServiceModel _productServiceModel;
        private TaskNotifier _taskNotifier;

        /// <summary>
        /// Command for cloning
        /// </summary>
        public ICommand DuplicateCmd { get; }

        /// <summary>
        /// Command for closing the dialog
        /// </summary>
        public ICommand CloseCmd { get; }

        /// <summary>
        /// Product to clone
        /// </summary>
        public ProductInfoViewModel Product { get; }

        /// <summary>
        /// Instance of the cloned product
        /// </summary>
        public ProductModel ClonedProduct { get; private set; }

        /// <summary>
        /// Task notifier to display a busy indicator
        /// </summary>
        public TaskNotifier TaskNotifier
        {
            get => _taskNotifier;
            private set
            {
                _taskNotifier = value;
                NotifyOfPropertyChange(nameof(TaskNotifier));
            }
        }

        private string _errorMessage;

        /// <summary>
        /// Displays general error messages if new revision creation is failed
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Given identifier
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Given revision
        /// </summary>
        public short Revision { get; set; }

        #endregion

        public DuplicateProductDialogViewModel(IProductServiceModel productServiceModel, ProductInfoViewModel product)
        {
            _productServiceModel = productServiceModel;
            Product = product;

            DuplicateCmd = new AsyncCommand(Duplicate, CanDuplicate, true);
            CloseCmd = new AsyncCommand(Close, _ => true, true);
        }

        /// <inheritdoc />
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializeAsync(cancellationToken);

            DisplayName = string.Format(Strings.DuplicateProductDialogViewModel_DisplayName, Product.Name);
        }

        private bool CanDuplicate(object parameters) =>
            !string.IsNullOrWhiteSpace(Identifier);

        private async Task Duplicate(object parameters)
        {
            ErrorMessage = string.Empty;

            try
            {
                var duplicateTask = _productServiceModel.DuplicateProduct(Product.Id, Identifier, Revision);
                TaskNotifier = new TaskNotifier(duplicateTask);

                var response = await duplicateTask;
                if (response.IdentityConflict || response.InvalidSource)
                {
                    ErrorMessage = Strings.DuplicateProductDialogViewModel_ConflictedIdentity;
                }
                else
                {
                    ClonedProduct = response.Duplicate;
                    await TryCloseAsync(true);
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        private Task Close(object obj) =>
            TryCloseAsync(false);
    }
}
