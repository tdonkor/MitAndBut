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
        

        public IRestResponse PostCheck(string url, string key, string contentType, string payLoad)
        {

            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", contentType);
            request.AddHeader("X-Flyt-API-Key", key);
            request.AddParameter(contentType, payLoad, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);
            return response;

        }

        //  public IRestResponse PostOrder(string payLoad)
        public IRestResponse PostOrder(string url, string key, string contentType, string payLoad)
        {
            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", contentType);
            request.AddHeader("X-Flypay-API-Key", key);
            request.AddParameter("text/plain", payLoad, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response;
        }

        //public IRestResponse Fullfillment(string payLoad, string orderId)
        public IRestResponse Fullfillment(string payLoad, string orderId)
        {

            var client = new RestClient("https://api.flypaythis.com/ordering/v3/order/" + orderId + "/fulfillment-type/collection-by-customer");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/plain");
            request.AddHeader("X-Flypay-API-Key", "u7f2r48x6bzwyy09vwsii");
            request.AddParameter("text/plain", payLoad, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            return response;
        }
    }
}
