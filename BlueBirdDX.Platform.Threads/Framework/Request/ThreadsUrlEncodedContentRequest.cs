using System.Reflection;
using DotNext;

namespace OatmealDome.Unravel.Framework.Request;

internal abstract class ThreadsUrlEncodedContentRequest : ThreadsRequest
{
    protected class ThreadsUrlEncodedParameterName : Attribute
    {
        public string ParameterName
        {
            get;
            private set;
        }

        public ThreadsUrlEncodedParameterName(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
    
    protected FormUrlEncodedContent CreateFormUrlEncodedContent()
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        
        foreach (PropertyInfo propertyInfo in this.GetType().GetProperties()
                     .Where(prop => prop.IsDefined(typeof(ThreadsUrlEncodedParameterName), false)))
        {
            object? value = propertyInfo.GetValue(this);
            
            string stringVal;

            if (value != null)
            {
                if (value.GetType().IsOptional())
                {
                    dynamic optional = value;

                    if (optional.IsUndefined)
                    {
                        continue;
                    }
                    
                    if (optional.IsNull)
                    {
                        stringVal = "null";
                    }
                    else
                    {
                        stringVal = value.ToString()!;
                    }
                }
                else
                {
                    stringVal = value.ToString()!;
                }
            }
            else
            {
                stringVal = "null";
            }
            
            ThreadsUrlEncodedParameterName attribute = propertyInfo.GetCustomAttribute<ThreadsUrlEncodedParameterName>()!;

            parameters[attribute.ParameterName] = stringVal;
        }
        
        return new FormUrlEncodedContent(parameters);
    }
}