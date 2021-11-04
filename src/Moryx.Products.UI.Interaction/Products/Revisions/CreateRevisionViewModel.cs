// Copyright (c) 2020, Phoenix Contact GmbH & Co. KG
// Licensed under the Apache License, Version 2.0

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using Moryx.ClientFramework.Commands;
using Moryx.ClientFramework.Dialog;
using Moryx.ClientFramework.Tasks;
using Moryx.Products.UI.Interaction.Properties;
using Moryx.Products.UI.ProductService;

namespace Moryx.Products.UI.Interaction
{
    internal class CreateRevisionViewModel : DialogScreen
    {
        private readonly IProductServiceModel _productServiceModel;
        private string _numberErrorMessage;
        private short _newRevision;
        private TaskNotifier _taskNotifier;
        private string _errorMessage;

        /// <summary>
        /// Command to create the new revision
        /// </summary>
        public ICommand CreateCmd { get; }

        /// <summary>
        /// Command for closing the dialog
        /// </summary>
        public ICommand CancelCmd { get; }

        private ProductModel[] _currentRevisions;

        public short NewRevision
        {
            get => _newRevision;
            set
            {
                _newRevision = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Task notifier to display a busy indicator
        /// </summary>
        public TaskNotifier TaskNotifier
        {
            get => _taskNotifier;
            private set
            {
                _taskNotifier = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Displays error message from revision number validation
        /// </summary>
        public string NumberErrorMessage
        {
            get => _numberErrorMessage;
            set
            {
                _numberErrorMessage = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Product to duplicate
        /// </summary>
        public ProductInfoViewModel Product { get; }

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
        /// Product which was be created after succeed OnCreateClick() call
        /// </summary>
        public ProductInfoViewModel CreatedProduct { get; private set; }

        /// <summary>
        /// Constructor for this view model
        /// </summary>
        public CreateRevisionViewModel(IProductServiceModel productServiceModel, ProductInfoViewModel product)
        {
            _productServiceModel = productServiceModel;
            Product = product;

            CreateCmd = new AsyncCommand(CreateRevision, CanCreateRevision, true);
            CancelCmd = new AsyncCommand(Cancel, CanCancel, true);
        }

        /// <inheritdoc />
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            await base.OnInitializeAsync(cancellationToken);

            DisplayName = Strings.CreateRevisionViewModel_DisplayName;

            var loadingTask = Task.Run(async delegate
            {
                try
                {
                    _currentRevisions = await _productServiceModel.GetProducts(new ProductQuery
                    {
                        Identifier =  Product.Identifier ,
                        RevisionFilter = RevisionFilter.All
                    }).ConfigureAwait(false);

                    Execute.OnUIThread(() => NewRevision = (short) (_currentRevisions.Max(pr => pr.Revision) + 1));
                }
                catch (Exception e)
                {
                    Execute.OnUIThread(() => ErrorMessage = e.Message);
                }
            }, cancellationToken);

            TaskNotifier = new TaskNotifier(loadingTask);
        }

        private bool CanCreateRevision(object arg)
        {
            if (TaskNotifier != null && !TaskNotifier.IsCompleted)
                return false;

            NumberErrorMessage = string.Empty;

            var isRevisionNumberFree = _currentRevisions.All(pr => pr.Revision != NewRevision);
            if (!isRevisionNumberFree)
            {
                NumberErrorMessage = Strings.CreateRevisionViewModel_RevisionNotAvailable;
                return false;
            }

            return true;
        }

        private async Task CreateRevision(object arg)
        {
            ErrorMessage = string.Empty;

            try
            {
                var duplicateTask = _productServiceModel.DuplicateProduct(Product.Id, Product.Identifier, NewRevision);
                TaskNotifier = new TaskNotifier(duplicateTask);

                var response =  await duplicateTask;
                if (response.IdentityConflict || response.InvalidSource)
                {
                    ErrorMessage = Strings.CreateRevisionViewModel_ConflictedIdentity;
                }
                else
                {
                    CreatedProduct = new ProductInfoViewModel(response.Duplicate);
                    await TryCloseAsync(true);
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        private bool CanCancel(object obj) =>
            !((AsyncCommand)CreateCmd).IsExecuting;

        private Task Cancel(object parameters) =>
            TryCloseAsync(false);
    }
}
