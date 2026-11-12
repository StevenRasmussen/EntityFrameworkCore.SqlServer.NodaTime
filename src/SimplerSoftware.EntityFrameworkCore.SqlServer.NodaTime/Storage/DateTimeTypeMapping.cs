using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Data;
using System.Data.Common;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage
{
    public abstract class DateTimeTypeMapping : RelationalTypeMapping
    {
        private const string DateFormatConst = "'{0:yyyy-MM-dd}'";
        private const string SmallDateTimeFormatConst = "'{0:yyyy-MM-ddTHH:mm:ss}'";
        private const string DateTimeFormatConst = "'{0:yyyy-MM-ddTHH:mm:ss.fff}'";

        private readonly string[] _dateTime2Formats =
        {
            "'{0:yyyy-MM-ddTHH:mm:ss}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.fK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.ffK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.fffK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.ffffK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.fffffK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.ffffffK}'",
            "'{0:yyyy-MM-ddTHH:mm:ss.fffffffK}'"
        };

        protected DateTimeTypeMapping(string storeType, Type clrType, ValueConverter valueConverter)
            : this(storeType, clrType, valueConverter, jsonValueReaderWriter: null)
        {
        }

        protected DateTimeTypeMapping(string storeType, Type clrType, ValueConverter valueConverter, JsonValueReaderWriter jsonValueReaderWriter)
            : base(CreateRelationalTypeMappingParameters(storeType, clrType, valueConverter, jsonValueReaderWriter))
        {
        }

        protected DateTimeTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters(string storeType, Type clrType, ValueConverter valueConverter, JsonValueReaderWriter jsonValueReaderWriter)
        {
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    clrType,
                    valueConverter,
                    jsonValueReaderWriter: jsonValueReaderWriter),
                storeType,
                StoreTypePostfix.Precision,
                storeType == SqlServerDateTimeTypes.DateTime2 ? System.Data.DbType.DateTime2 : System.Data.DbType.DateTime);
        }

        protected override void ConfigureParameter(DbParameter parameter)
        {
            base.ConfigureParameter(parameter);

            // Workaround for a SQLClient bug
            if (DbType == System.Data.DbType.Date)
            {
                ((SqlParameter)parameter).SqlDbType = SqlDbType.Date;
            }

            if (Size.HasValue
                && Size.Value != -1)
            {
                parameter.Size = Size.Value;
            }
        }

        protected override string SqlLiteralFormatString
        {
            get
            {
                switch (StoreType)
                {
                    case SqlServerDateTimeTypes.Date:
                        return DateFormatConst;
                    case SqlServerDateTimeTypes.DateTime:
                        return DateTimeFormatConst;
                    case SqlServerDateTimeTypes.SmallDateTime:
                        return SmallDateTimeFormatConst;
                    default:
                        if (Size.HasValue)
                        {
                            var size = Size.Value;
                            if (size <= 7
                                && size >= 0)
                            {
                                return _dateTime2Formats[size];
                            }
                        }

                        return _dateTime2Formats[7];
                }
            }
        }
    }
}
