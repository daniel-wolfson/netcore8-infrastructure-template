using Custom.Framework.Contracts;

namespace Custom.Framework.Models.Base
{
    public class OptimaSimpleResult<TData> : OptimaResult, IOptimaResult<TData> where TData : class
    {
        public OptimaSimpleResult(IServiceProvider sp): base() { }

        //public new TData? Data { get; set; }

        public TData? Data { get; set; }

        //public OptimaSimpleResult(IServiceProvider sp, string message) : this(sp, message, default)
        //{
        //}
        //public OptimaSimpleResult(IServiceProvider sp, string message, TData? data, string? status = null) : base(sp)
        //{
        //    Message = new OptimaMessage { Text = $"{message}.{(!string.IsNullOrEmpty(status) ? $" Status:{status}" : "")}" };
        //    Data = data;
        //}

        public override bool IsSuccess => Error == false;
    }
}