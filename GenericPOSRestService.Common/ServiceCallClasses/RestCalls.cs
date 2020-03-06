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
            request.AddParameter("text/plain",payLoad, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);
            return response;

        }

        public IRestResponse PostOrder(string payLoad)
        {
            var client = new RestClient("https://api.flypaythis.com/ordering/v3/order");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Connection", "keep-alive");
            request.AddHeader("Host", "api.flypaythis.com");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Content-Type", "text/plain");
            request.AddHeader("X-Flypay-API-Key", "u7f2r48x6bzwyy09vwsii");
            request.AddParameter("text/plain", payLoad, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response;
        }


        //public IRestResponse Fullfillment(string payLoad, string orderId)
        //{


        //    var client = new RestClient("https://api.flypaythis.com/ordering/v3/order/" + orderId + "/fulfillment-type/collection-by-customer");
        //    var request = new RestRequest(Method.POST);
        //    request.AddHeader("Connection", "keep-alive");
        //    request.AddHeader("Host", "api.flypaythis.com");
        //    request.AddHeader("Accept", "*/*");
        //    request.AddHeader("Content-Type", "text/plain");
        //    request.AddHeader("X-Flypay-API-Key", "u7f2r48x6bzwyy09vwsii");
        //    request.AddParameter("text/plain", payLoad, ParameterType.RequestBody);
        //    IRestResponse response = client.Execute(request);
        //    return response;
        //}
    }
}
