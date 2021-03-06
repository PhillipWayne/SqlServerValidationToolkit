﻿using log4net;
using SqlServerValidationToolkit.Model.Context;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlServerValidationToolkit.Model.Entities.Rule
{
    [Table("Validation_ValidationRule")]
    public abstract class ValidationRule
    {
        ILog _log = LogManager.GetLogger(typeof(ValidationRule));

        public const string NotEnteredErrorTypeCode = "NotEntered";

        public ValidationRule()
        {
            this.Validation_WrongValue = new HashSet<WrongValue>();
            this.Errortypes = new HashSet<ErrorType>();
            this.IsActive = true;
        }

        [Key]
        public int ValidationRule_id { get; set; }
        public int Column_fk { get; set; }
        public bool IsActive { get; set; }
        
        public Nullable<NullValueTreatment> NullValueTreatment { get; set; }
        public string CompiledQuery { get; set; }

        [ForeignKey("Column_fk")]
        public virtual Column Column { get; set; }
        public virtual ICollection<WrongValue> Validation_WrongValue { get; set; }
        public virtual ICollection<ErrorType> Errortypes { get; set; }
        public virtual string ErrorDescriptionFormat
        {
            get
            {
                return "{0}";
            }
        }

        /// <summary>
        /// The Query the returns the id and the errorId of the invalid entries
        /// </summary>
        public abstract string Query
        {
            get;
        }


        /// <summary>
        /// Returns the check if the column is null depending on the NullValueTreatment
        /// </summary>
        protected string GetNullCheck()
        {
            string columnName = GetColumnName();
            string nullCheck = "";
            switch (NullValueTreatment)
            {
                case Entities.NullValueTreatment.Ignore:
                    nullCheck = string.Format("AND {0} IS NOT NULL", columnName);
                    break;
                case Entities.NullValueTreatment.InterpretAsError:
                    nullCheck = string.Format("OR {0} IS NULL", columnName);
                    break;
                case Entities.NullValueTreatment.ConvertToDefaultValue:
                    nullCheck = "";
                    break;
                default:
                    throw new NotImplementedException();
            }
            return nullCheck;
        }

        /// <summary>
        /// Returns the case inside the switch-statement if the value is null and returns the correct code
        /// </summary>
        protected string GetNullCase()
        {
            return NullValueTreatment.HasValue && NullValueTreatment.Value == Entities.NullValueTreatment.InterpretAsError ?
                string.Format("WHEN ({0} IS NULL) THEN '{1}'",
                    Column.Name, NotEnteredErrorTypeCode
                    ) : "WHEN (1=0) THEN NULL";
        }

        /// <summary>
        /// Returns the column name or the default value for null-values which should be converted
        /// </summary>
        protected string GetColumnName()
        {
            string columnName = Column.Name;
            if (NullValueTreatment == Entities.NullValueTreatment.ConvertToDefaultValue)
            {
                //the default value is dependent of the column-type
                string defaultValue;

                if (Column.IsNumeric)
                {
                    defaultValue = "0";
                }
                else if (Column.IsDateTime)
                {
                    defaultValue = "GETDATE()";
                } else
                {
                    defaultValue = "''";
                }

                
                columnName = string.Format("ISNULL({0},{1})", columnName,defaultValue);
            }
            return columnName;
        }

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            return new List<ValidationResult>();
        }

        /// <summary>
        /// Fills the wrongValues-table with all wrong values from the rule
        /// </summary>
        public void Validate(System.Data.Common.DbConnection connection, SqlServerValidationToolkitContext ctx)
        {
            if (connection.State==System.Data.ConnectionState.Closed)
            {
                connection.Open();
            }

            

            try
            {
                if (!IsActive)
                {
                    SetAllWrongValuesToCorrected(connection);
                }
                else
                {
                    UpdateWrongValues(connection, ctx);
                }
                
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception occurred while executing '{0}'", CompiledQuery),e);
            }
            finally
            {
                connection.Close();
            }
        }

        private void UpdateWrongValues(System.Data.Common.DbConnection connection, SqlServerValidationToolkitContext ctx)
        {
            string q = CompiledQuery;
            var c = connection.CreateCommand();
            c.CommandText = q;
            var reader = c.ExecuteReader();

            List<WrongValue> correctedWrongValues = Validation_WrongValue.ToList();

            while (reader.Read())
            {
                //the query returns the id of the invalid value and the errortype-id
                int invalidValueId = reader.GetInt32(0);

                string errorTypeCode = reader.GetString(1);


                WrongValue existingWrongValue = Validation_WrongValue
                    .SingleOrDefault(wv=>
                    wv.Errortype.CodeForValidationQueries==errorTypeCode 
                    &&
                    wv.Id == invalidValueId
                    );

                string value = GetValue(invalidValueId, connection);

                if (existingWrongValue==null)
                {
                    ErrorType errorType = ctx.Errortypes.Where(et => et.CodeForValidationQueries == errorTypeCode).SingleOrDefault();

                    if (errorType==null) //errorType should exist
                    {
                        throw new Exception("ErrorType not found for code '" + errorTypeCode+"'");
                    }
                    
                    WrongValue wrongValue = new WrongValue()
                    {
                        Errortype = errorType,
                        Id = invalidValueId,
                        Value = value
                    };
                    Validation_WrongValue.Add(wrongValue);
                }
                else
                {
                    existingWrongValue.Value = value;
                    correctedWrongValues.Remove(existingWrongValue);
                }
            }

            _log.Info(string.Format("{0} wrong values are corrected", correctedWrongValues.Count));
            foreach (var wvCorrected in correctedWrongValues)
            {
                wvCorrected.Is_Corrected = true;
            }
        }

        private void SetAllWrongValuesToCorrected(System.Data.Common.DbConnection connection)
        {
            foreach (var wrongValue in Validation_WrongValue)
            {
                wrongValue.Is_Corrected = true;
            }
        }


        /// <summary>
        /// Returns the value of the column for the id
        /// </summary>
        private string GetValue(int invalidValueId, System.Data.Common.DbConnection connection)
        {
            string selectValueSqlFormat = "SELECT [{0}] FROM [{1}] WHERE [{2}]={3}";
            string selectValueSql = string.Format(selectValueSqlFormat,
                Column.Name,
                Column.Source.Name,
                Column.Source.IdColumnName,
                invalidValueId);
            var c = connection.CreateCommand();
            c.CommandText = selectValueSql;
            var result = c.ExecuteScalar();
            return result.ToString();
        }
    }
}
