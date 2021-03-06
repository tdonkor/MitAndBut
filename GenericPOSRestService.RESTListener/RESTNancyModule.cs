﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nancy;
using System.Diagnostics;
using System.Threading.Tasks;
using GenericPOSRestService.Common;
using GenericPOSRestService.Common.ServiceCallClasses;
using Newtonsoft.Json.Linq;
using Nancy.Responses;
using Newtonsoft.Json;
using RestSharp;
using System.Xml.Linq;
using System.Data.SqlClient;
using System.Data;

namespace GenericPOSRestService.RESTListener
{
    /// <summary>The REST listener module</summary>
    public class RESTNancyModule : NancyModule
    {
        /// <summary>Formatted string for writing in the log on a service request</summary>
        private const string LogRequestString = "REST service call \"{0}\" => request: {1}";

        /// <summary>Formatted string for writing in the log on a service response</summary>
        private const string LogResponseString = "REST service call \"{0}\" =>\r\trequest: {1}\r\tresponse: {2}\r\tCalculationTimeInMilliseconds: {3}";

        private const string LogResponseSkipRequestString = "REST service call \"{0}\" => response: {2}\r\tCalculationTimeInMilliseconds: {3}";

        
        //HeaderDetails
        public static string ContentType;

        // Security Key Types
        public static string FlytKeyType1;
        public static string FlytKeyType2;

        // Security Key values
        public static string FlytAPIKey1;
        public static string FlytAPIKey2;

        //APICalls
        public static string CheckBasketUrl;
        public static string OrderUrl;
        public static string FullFillmentUrl;


        //Connection String
        public static string ConnectionString;
        public static string TableName;

        public string LogName
        {
            get
            {
                return ServiceListener.Instance.LogName;
            }
        }

        public RESTNancyModule()
            : base(ListenerConfig.Instance.POSRESTModuleBase)
        {
            Get["/status/{kiosk?}"] = parameters =>
            {
                // try to get the kiosk parameter
                string kiosk = null;

                try
                {
                    string kioskStr = parameters.kiosk;

                    if (!string.IsNullOrWhiteSpace(kioskStr))
                    {
                        kiosk = kioskStr;
                    }
                }
                catch
                {
                }

                // defines the function for calling GetStatus method
                Func<string, IPOSResponse> func = (bodyStr) =>
                {
                    StatusPOSResponse statusPOSResponse = new StatusPOSResponse();

                    if (string.IsNullOrWhiteSpace(kiosk))
                    {
                        // the kiosk parameter was not specified
                        statusPOSResponse.SetPOSError(Errors.KioskNotSpecified);
                    }
                    else
                    {
                        try
                        {
                            // call the POS and get the status for the specified kiosk
                            statusPOSResponse = GetStatus(kiosk);
                        }
                        catch (Exception ex)
                        {
                            statusPOSResponse = new StatusPOSResponse();
                            statusPOSResponse.SetPOSError(Errors.POSError, ex.Message);
                        }
                    }

                    return statusPOSResponse;
                };

                // call GetStatus function
                IPOSResponse response = ExecuteRESTCall(func);

                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    return new TextResponse(response.HttpStatusCode, response.ResponseContent);
                }
                else
                {
                    return response.HttpStatusCode;
                }
            };

            Post["/order"] = parameters =>
            {
                // defines the function for calling OrderTransaction method
                Func<string, IPOSResponse> func = (bodyStr) =>
                {
                    OrderCreatePOSResponse posResponse = new OrderCreatePOSResponse();
                    Order order = posResponse.OrderCreateResponse.Order;
                    OrderCreateRequest request = null;

                    try
                    {
                        // deserialize request
                        request = JsonConvert.DeserializeObject<OrderCreateRequest>(bodyStr);
                    }
                    catch(Exception ex)
                    {
                        posResponse.SetPOSError(Errors.ErrorDeserializeRequest, ex.Message);
                    }

                    if (!order.HasErrors)
                    {
                        // no deserialize errors => check some elements
                        if (request.DOTOrder == null)
                        {
                            posResponse.SetPOSError(Errors.OrderMissing);
                        }
                        else if (string.IsNullOrWhiteSpace(request.DOTOrder.Kiosk))
                        {
                            posResponse.SetPOSError(Errors.KioskNotSpecified);
                        }
                        else if (string.IsNullOrWhiteSpace(request.DOTOrder.RefInt))
                        {
                            posResponse.SetPOSError(Errors.RefIntNotSpecified);
                        }
                        else if (request.DOTOrder.IsNewOrder && !request.DOTOrder.Items.Any())
                        {
                            posResponse.SetPOSError(Errors.ItemListNotSpecified);
                        }
                        else if (request.DOTOrder.IsTenderOrder
                            && ((request.DOTOrder.Tender == null)
                                || (request.DOTOrder.Tender.TenderItems == null)
                                || !request.DOTOrder.Tender.TenderItems.Any()))
                        {
                            posResponse.SetPOSError(Errors.TenderItemListNotSpecified);
                        }
                        else if (request.DOTOrder.IsExistingOrder && string.IsNullOrWhiteSpace(request.DOTOrder.OrderID))
                        {
                            posResponse.SetPOSError(Errors.OrderIDNotSpecified);
                        }
                    }

                    if (!order.HasErrors)
                    {
                        try
                        {
                            posResponse = OrderTransaction(request);
                        }
                        catch (Exception ex)
                        {
                            posResponse = new OrderCreatePOSResponse();
                            posResponse.SetPOSError(Errors.POSError, ex.Message);
                        }
                    }

                    return posResponse;
                };

                // call OrderTransaction method
                IPOSResponse response = ExecuteRESTCall(func);

                if (response.HttpStatusCode == HttpStatusCode.Created)
                {
                    return new TextResponse(response.HttpStatusCode, response.ResponseContent);
                }
                else
                {
                    return response.HttpStatusCode;
                }
            };

            Get["/testdiag/{culturename?}"] = parameters =>
            {
                // try to get the culture name
                string culturename = null;

                try
                {
                    culturename = parameters.culturename;
                }
                catch
                {
                }

                // defines the function for calling TestDiag method
                Func<string, IPOSResponse> func = (bodyStr) =>
                {
                    TestDiagPOSResponse posResponse = new TestDiagPOSResponse();

                    if (string.IsNullOrWhiteSpace(culturename))
                    {
                        posResponse.SetPOSError(Errors.CultureNameNotSpecified);
                    }
                    else
                    {
                        try
                        {
                            posResponse = TestDiag(culturename);
                        }
                        catch (Exception ex)
                        {
                            posResponse = new TestDiagPOSResponse();
                            posResponse.SetPOSError(Errors.POSError, ex.Message);
                        }
                    }

                    return posResponse;
                };

                // call TestDiag method
                IPOSResponse response = ExecuteRESTCall(func);

                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    return new TextResponse(response.HttpStatusCode, response.ResponseContent);
                }
                else
                {
                    return response.HttpStatusCode;
                }
            };
        }

        /// <summary>Writes the message to the log file</summary>
        /// <param name="message">The message</param>
        /// <param name="methodName">The method</param>
        /// <param name="requestContent">The request content</param>
        /// <param name="level">The log level</param>
        private void WriteLog(
            string message,
            string methodName,
            string requestContent,
            LogLevel level)
        {
            // write the message
            switch (level)
            { 
                case LogLevel.Debug:
                    Log.Debug(LogName, message);
                    break;

                case LogLevel.Error:
                    Log.Error(LogName, message);
                    break;

                case LogLevel.Warnings:
                    Log.Warnings(LogName, message);
                    break;

                case LogLevel.Info:
                    Log.Info(LogName, message);
                    break;

                case LogLevel.Windows:
                    Log.WindowsError(LogName, message);
                    break;

                default:
                    Log.Sys(LogName, message);
                    break;
            }

            // raise OnWriteToLog event
            ServiceListener.Instance.OnWriteToLog(new WriteToLogEventArgs
            {
                MethodName = methodName,
                RequestContent = requestContent,
                Message = message
            });
        }

        /// <summary>Generic method for execute the REST call</summary>
        /// <param name="func">The cal REST function </param>
        private IPOSResponse ExecuteRESTCall(System.Func<string, IPOSResponse> func)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string bodyStr = Request.Body.ReadAsString();
            string restUrl = Request.Url.ToString();
            string requestIP = Request.UserHostAddress;

            if (requestIP == "::1")
            {
                requestIP = "localhost";
            }

            int lastIndex = Request.Url.Path.LastIndexOf('/');

            string methodName = lastIndex >= 0 ? Request.Url.Path.Substring(lastIndex + 1) : Request.Url.Path;

            string logRequestString = AddPrefixMessage(
                GetLogRequestString(restUrl, bodyStr),
                requestIP);

            // log request
            WriteLog(logRequestString, Request.Method, bodyStr, LogLevel.Debug);

            // call the function
            IPOSResponse response = func(bodyStr);

            sw.Stop();

            string logResponseString = AddPrefixMessage(
                GetLogResponseString(restUrl, bodyStr, response.ResponseContent, sw.ElapsedMilliseconds),
                requestIP);

            // log response
            WriteLog(logResponseString, Request.Method, bodyStr, LogLevel.Debug);

            return response;
        }

        private string AddPrefixMessage(string message, string requestIP)
        {
            string prefixMsg = "";

            if (!string.IsNullOrWhiteSpace(requestIP))
            {
                prefixMsg = string.Format("Request IP: {0}", requestIP);
            }

            return prefixMsg + (string.IsNullOrWhiteSpace(prefixMsg) ? "" : ", ") + message;
        }

        /// <summary>Returns the request message for writing in the log file</summary>
        /// <param name="url">The url</param>
        /// <param name="request">The request</param>
        private string GetLogRequestString(string url, string request)
        {
            return string.Format(LogRequestString, url, request);
        }

        /// <summary>Returns the response message for writing in the log file</summary>
        /// <param name="url">The url</param>
        /// <param name="response">The response</param>
        private string GetLogResponseString(string url, string request, string response, long calculationTimeInMilliseconds, bool skipRequest = false)
        {
            return string.Format(skipRequest ? LogResponseSkipRequestString : LogResponseString, url, request, response, calculationTimeInMilliseconds);
        }

        /// <summary>Call the POS for GetStatus method</summary>
        /// <param name="kiosk">The kiosk id</param>
        public StatusPOSResponse GetStatus(string kiosk)
        {
            StatusPOSResponse response = new StatusPOSResponse();
            string responseStr = string.Empty;
            StatusResponse getResponse;
           

            //check kiosk is valid
            if (string.IsNullOrWhiteSpace(kiosk))
            {
                // the kiosk parameter was not specified
                response.SetPOSError(Errors.KioskNotSpecified);
            }
            else
            {
                // POS Calls - Get the status load the url path
                LoadAPIUrls();

                //Deserialise returned data into a JSon object to return
                getResponse = JsonConvert.DeserializeObject<StatusResponse>(responseStr);
                response.StatusResponse = getResponse;
            }

            return response;
        }

        /// <summary>Call the POS for Order method</summary>
        /// <param name="request">The request</param>
        public OrderCreatePOSResponse OrderTransaction(OrderCreateRequest request)
        {
            OrderCreatePOSResponse response = new OrderCreatePOSResponse();
            HttpStatusCode httpStatusCode = response.HttpStatusCode;
            Order order = response.OrderCreateResponse.Order;
            string responseStr = string.Empty;
            string orderNum = string.Empty;
            int tax = 0;
            int basketId = 0;

            //copy the TableServiceNumber to the tableNo
            if ((request.DOTOrder.Location == Location.EatIn) && (request.DOTOrder.TableServiceNumber != null))
                request.DOTOrder.tableNo = Convert.ToInt32(request.DOTOrder.TableServiceNumber);

            string requestStr = JsonConvert.SerializeObject(request.DOTOrder);

            /****************************
            * Load the API settings to use
            * ****************************/
            LoadAPIUrls();


            /**************************************************************
             * Functions
             * ***********************************************************/
            if (request.DOTOrder.FunctionNumber == FunctionNumber.PRE_CALCULATE)
            {
                //Do CheckBasket
            
                CallStoredProcs procs = new CallStoredProcs(request, response);
                response = procs.CheckBasketStoredProcs();

                if (response == null)
                {
                    Log.Error("Error in CheckBasket");
                    return response;
                }


            }

            if (request.DOTOrder.FunctionNumber == FunctionNumber.EXT_OPEN_ORDER)
            {
                CallStoredProcs procs = new CallStoredProcs(request, response);
                //response = procs.();

                if (response == null)
                {
                    Log.Error("Error in CheckBasket");
                    return response;
                }
            }


                if (request.DOTOrder.FunctionNumber == FunctionNumber.EXT_COMPLETE_ORDER)
            {

                response.OrderCreateResponse.Order.Kiosk = request.DOTOrder.Kiosk;
                response.OrderCreateResponse.Order.RefInt = request.DOTOrder.RefInt;
                response.OrderCreateResponse.Order.OrderID = request.DOTOrder.OrderID;
                response.OrderCreateResponse.Order.Totals.AmountPaid = Convert.ToInt64(request.DOTOrder.PaidAmount);

                //TODO get the Order number and the tax from the database.
                using (SqlConnection con = new SqlConnection())
                {
                    con.ConnectionString = ConnectionString;
                    con.Open();
                    SqlCommand comm = new SqlCommand($"SELECT ID, APIPosOrderID, APItax from {TableName } where KioskRefInt = {Convert.ToInt32(request.DOTOrder.RefInt)}", con);
                    using (SqlDataReader reader = comm.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            basketId = reader.GetInt32(0);
                            orderNum = Convert.ToString(reader.GetInt32(1));
                            tax = reader.GetInt32(2);
                        }
                    }

                    if (string.IsNullOrEmpty(orderNum))
                    {
                        Log.Error($"No Order Number returned from the database for Transaction {request.DOTOrder.RefInt}");
                    }
                    else
                    {
                        Log.Info($"Order number = {orderNum} for Transaction {request.DOTOrder.RefInt}");
                    }

                    //close the basket

                    //Console.WriteLine($"Basket for ID: {basketId} = " + ExecuteOrderBasketClose(con, basketId));
                }

                response.OrderCreateResponse.Order.OrderPOSNumber = Convert.ToInt64(orderNum);
               
            }

            //copy to Order Table Number
            if ((request.DOTOrder.Location == Location.EatIn) && (request.DOTOrder.TableServiceNumber != null))
            {
                response.OrderCreateResponse.Order.tableNo = Convert.ToInt32(request.DOTOrder.TableServiceNumber);
            }


            if (httpStatusCode == HttpStatusCode.Created)
            {
                Log.Info($"HTTP Status Code Created:{httpStatusCode}");
            }
            else
            {
                Log.Error($"HTTP Status Code:{httpStatusCode}");
            }

            return response;
        }

        /// <summary>Call the POS for TestDiag method</summary>
        /// <param name="cultureName">The culture name</param>
        public TestDiagPOSResponse TestDiag(string cultureName)
        {
            TestDiagPOSResponse response = new TestDiagPOSResponse();

            // TODO: call (calls) to POS

            return response;
        }

        /// <summary>
        /// This method gets the customer API details from the 
        /// C:\Acrelec\AcrBridgeService\APISettingsConfig file
        /// </summary>
        private void LoadAPIUrls()
        {
            try
            {
                string filePath = Properties.Settings.Default.ApiSettingsConfigFileName;
                XElement elements = XElement.Load(filePath);

                //Header details
                XElement contentTypeElement = elements.Element("ContentType");

                //Key types
                XElement flytKeyTypeElement1 = elements.Element("KeyType1");
                XElement flytKeyTypeElement2 = elements.Element("KeyType2");

                //Key details
                XElement flytAPIKey1Element = elements.Element("APIKey1");
                XElement flytAPIKey2Element = elements.Element("APIKey2");

                //API details
                XElement checkBasketUrlElement = elements.Element("CheckBasketURL");
                XElement orderUrlElement = elements.Element("OrderURL");
                XElement fullFillmentUrlElement = elements.Element("FullFillmentURL");


                // Database details
                XElement connectionStringElement = elements.Element("ConnectionString");
                XElement tableNameElement = elements.Element("TableName");

                /******************************************************
                 * Set the static values to use
                 * ****************************************************/
                //HeaderDetails
                ContentType = contentTypeElement.Value;
                FlytAPIKey1 = flytAPIKey1Element.Value;
                FlytAPIKey2 = flytAPIKey2Element.Value;
                FlytKeyType1 = flytKeyTypeElement1.Value;
                FlytKeyType2 = flytKeyTypeElement2.Value;

                //API Url Calls
                CheckBasketUrl = checkBasketUrlElement.Value;
                OrderUrl = orderUrlElement.Value;
                FullFillmentUrl = fullFillmentUrlElement.Value;


                //Database Details
                ConnectionString = connectionStringElement.Value;
                TableName = tableNameElement.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }

        private string ExecuteOrderBasketClose(SqlConnection con, int orderBasketID)
        {
            string payload = "-9";
            // Create and configure a command object
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasketClose";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (string)reader[0];
                    payload = result;
                }
            }
            Log.Info("Execute OrderBasketClose - OrderBasketID = {0}: {1}", orderBasketID, payload);
            return payload;
        }
    }
}
