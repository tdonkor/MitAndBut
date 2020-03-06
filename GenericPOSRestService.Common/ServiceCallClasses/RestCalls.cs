using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using RestSharp;

namespace GenericPOSRestService.Common.ServiceCallClasses
{
    public class RestCalls
    {
        

        public IRestResponse PostCheckBasket(string payLoad)
        {

            var client = new RestClient("https://flyt-acrelec-integration.flyt-platform.com/checkBasket");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Host", "flyt-acrelec-integration.flyt-platform.com");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Content-Type", "text/plain");
            request.AddHeader("X-Flyt-API-Key", "hdgskIZRgBmyArKCtzkjkZIvaBjMkXVbWGvbq");
            request.AddParameter(payLoad, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);
            return response;

        }

    }
}
