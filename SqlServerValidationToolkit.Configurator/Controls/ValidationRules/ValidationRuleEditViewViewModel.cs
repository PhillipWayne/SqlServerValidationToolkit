﻿using GalaSoft.MvvmLight.Command;
using SqlServerValidationToolkit.Model.Entities;
using SqlServerValidationToolkit.Model.Entities.Rule;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SqlServerValidationToolkit.Configurator.Controls.ValidationRules
{
    public abstract class ValidationRuleEditViewViewModel : SelectableViewModel, IDataErrorInfo
    {
        public ValidationRuleEditViewViewModel(ValidationRule rule)
        {
            _rule = rule;

            AddNewErrorTypeCommand = new RelayCommand(() => AddNewErrorType(), () => _rule is CustomQueryRule);
            DeleteSelectedErrorTypeCommand = new RelayCommand(() => DeleteSelectedErrorType(), () => _rule is CustomQueryRule);
        }
        public bool RuleIsCustomQueryRule
        {
            get
            {
                return Rule is CustomQueryRule;
            }
        }
        public bool ErrorTypeIsSelected
        {
            get
            {
                return SelectedErrorType != null;
            }
        }

        private void AddNewErrorType()
        {
            var et = new ErrorType()
            {
                Check_Type = "CustomQuery", //TODO: Make it a constant
                Description = string.Empty
            };

            _rule.Errortypes.Add(et);
            RaisePropertyChanged(() => ErrorTypes);
            RaisePropertyChanged(() => CustomeQueryRuleContainsErrorTypesWithNonUniqueIds);
        }

        public bool CustomeQueryRuleContainsErrorTypesWithNonUniqueIds
        {
            get
            {
                //TODO: Implement
                return false;
                //return _rule.Errortypes.Any(et => et.ErrorType_id == 0);
            }
        }

        private void DeleteSelectedErrorType()
        {
            _rule.Errortypes.Remove(SelectedErrorType);
            //Delete errorType
            RaisePropertyChanged(() => ErrorTypes);
            RaisePropertyChanged(() => CustomeQueryRuleContainsErrorTypesWithNonUniqueIds);
        }

        private ErrorType _selectedErrorType;
        public ErrorType SelectedErrorType
        {
            get { return _selectedErrorType; }
            set
            {
                _selectedErrorType = value;
                RaisePropertyChanged(() => ErrorTypeIsSelected);
            }
        }

        private ValidationRule _rule;

        public ValidationRule Rule
        {
            get
            {
                return _rule;
            }
        }

        public string Header
        {
            get
            {
                return _rule.ToString();
            }
        }

        public IEnumerable<NullValueTreatment> NullValueTreatments
        {
            get
            {
                return Enum.GetValues(typeof(NullValueTreatment)).Cast<NullValueTreatment>();
            }
        }

        public NullValueTreatment? SelectedNullValueTreatment
        {
            get
            {
                return _rule.NullValueTreatment;
            }
            set
            {
                _rule.NullValueTreatment = value;
                RecompileQuery();
            }
        }

        public string Query
        {
            get
            {
                return Rule.CompiledQuery;
            }
        }

        public List<ErrorType> ErrorTypes
        {
            get
            {
                return Rule.Errortypes.ToList();
            }
        }
        /// <summary>
        /// Query is recompiled and saved in the CompiledQuery-property
        /// </summary>
        protected void RecompileQuery()
        {
            Rule.CompiledQuery = Rule.Query;
            RaisePropertyChanged(() => Query);
        }

        public ICommand AddNewErrorTypeCommand { get; set; }
        public ICommand DeleteSelectedErrorTypeCommand { get; set; }


        public string Error
        {
            get { return this[string.Empty]; }
        }

        public string this[string columnName]
        {
            get
            {
                string aggregatedValidationResult = "";
                var validationResults = this._rule.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(_rule));
                foreach (var validationResult in validationResults)
                {
                    if (columnName == string.Empty || validationResult.MemberNames.Contains(columnName))
                    {
                        aggregatedValidationResult += validationResult.ErrorMessage + Environment.NewLine;
                    }
                }
                return aggregatedValidationResult;
            }
        }

        internal void NotifyErrorTypeChanged()
        {
            RaisePropertyChanged(() => CustomeQueryRuleContainsErrorTypesWithNonUniqueIds);
        }
    }
}
