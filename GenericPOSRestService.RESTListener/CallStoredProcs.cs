using GenericPOSRestService.Common;
using GenericPOSRestService.Common.ServiceCallClasses;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericPOSRestService.RESTListener
{
    public class CallStoredProcs
    {
        OrderCreateRequest request;
        OrderCreatePOSResponse response;
        int basketId = 0;
        string payLoad = string.Empty;
        RestCalls restCalls;

        /// <summary>
        /// Get the order request and populate the response and return it after processing the request
        /// </summary>
        /// <param name="orderRequest"></param>
        /// <param name="orderResponse"></param>
        public CallStoredProcs(OrderCreateRequest orderRequest, OrderCreatePOSResponse orderResponse)
        {
            this.request = orderRequest;
            this.response = orderResponse;
            restCalls = new RestCalls();
        }

        public CallStoredProcs() { }

        /// <summary>
        /// 
        /// </summary>
        public void CheckBasketStoredProcs()
        {
            // Create a new SqlConnection object
            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = RESTNancyModule.ConnectionString;
                con.Open();
                Log.Info("Connected to the Database");

                /****  BASKETID ***************************************************************
                * 1) Execute the OrderBasketAdd stored proc to get the Id for the new Basket
                * this must be called first for a new transaction
                * ******************************************************************************/
                basketId = ExecuteOrderBasketAdd(con);

                if (basketId < 1)
                {
                    Log.Error("Order Basket Add Failed ");
                    return;
                }
                Log.Info($"BasketId = {basketId}");

                /*****  ADDITEMS **************************************************************
                * 2) Call the OrderBasketAddItem stored proc to get the parentId for all items
                * ******************************************************************************/
                for (int i = 0; i < request.DOTOrder.Items.Count; i++)
                {
                    Item item = request.DOTOrder.Items[i];
                    ProcessItem(con, item, basketId, 0);
                }

                /****  CHECKBASKET  ************************************************************
                * 3) Execute the stored proc OrderBasket_API_CheckBasket to get the payload
                * *******************************************************************************/
                payLoad = ExecuteOrderBasket_API_Checkbasket(con, basketId);

                /**  *****CHECKBASKET ******************************************************
                 * 3a) Get the CheckBasket API Response call 
                 * ***************************************************************************/
                IRestResponse checkBasketResp = restCalls.PostCheckBasket(payLoad);
                      
                /****** CHECKBASKET ***********************************************************
                * 3b) Execute the store proc OrderBasket_APIResponse_CheckBasket this will
                * Insert the response of the OrderBasket_API_CheckBasket Store Proc 
                * 
                * ************************************************************************************/
                ExecuteOrderBasket_APIResponse_Checkbasket(con, basketId, checkBasketResp.Content.ToString());

                /****  ORDER ***************************************************************************
                 * 4) Execute the store proc Generate payload for Order API Call
                 * 
                 * ******************************************************************************/
                 payLoad = string.Empty;
                 payLoad = ExecuteOrderBasket_API_Order(con, basketId);

                /****  ORDER *******************************************************************
                * 4a)  Get the Order API Response call 
                * ******************************************************************************/
                IRestResponse checkOrderResp = restCalls.PostOrder(payLoad);

                /****  ORDER *************************************************************************
                 * 4b)   Insert the response from the Order api call into the OrderBasketAPIResponse 
                 ************************************************************************************/
                // Update orderBasket with API Response              
                ExecuteOrderBasket_APIResponse_Order(con, basketId, checkOrderResp.Content.ToString());

                /*****  FULFILLMENT  **************************************************************************
                 *  5) Execute the store proc OrderBasket_API_CollectionByCustomer this will
                 * Generate  the payload for Collection By Customer API Call
                 * *****************************************************************************/
                payLoad = string.Empty;
                payLoad = ExecuteOrderBasket_API_CollectionByCustomer(con, basketId);

                /****  FULFILLMENT *******************************************************************
                * 5a)  Get the Order API Response call 
                * ******************************************************************************/
                IRestResponse checkFullfilmentResp = restCalls.PostOrder(payLoad);

                /********FULFILLMENT ****************************************************************
                *  Update orderBasket with API Response  
                * ******************************************************************************/
                ExecuteOrderBasket_APIResponse_CollectionByCustomer(con, basketId, checkFullfilmentResp.ToString());
            }

        }



        private void ProcessItem(SqlConnection con,  Item parentItem, int basketID, int parentId)
        {

            int itemID = -9;

            Int64 parentItemID = Convert.ToInt64(parentItem.ID);
            Int32 parentItemQty = Convert.ToInt32(parentItem.Qty);

            // get the parent Id for the item use the basketId, AKDURN, QTY, leave the parentId at 0 for the first call
            int result1 = ExecuteOrderBasketAddItem(con,
                basketId,
                parentItemID,
                parentItemQty,
                parentId);

            itemID = result1;

           if (itemID > 0)
            {
                if (parentItem.Items.Count > 0)
                {
                    for (int j = 0; j < parentItem.Items.Count; j++)
                    {
                        Item childItem = parentItem.Items[j];

                        ProcessItem(con, childItem, basketID, itemID);
                    }
                }
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="con"></param>
        /// <returns></returns>
        private int ExecuteOrderBasketAdd(SqlConnection con)
        {
            int orderBasketID = -9;
            // Create and configure a command object        
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasketAdd";
            com.Parameters.Add("@kioskRefInt", SqlDbType.Int).Value = request.DOTOrder.RefInt;
            com.Parameters.Add("@kioskID", SqlDbType.Int).Value = request.DOTOrder.Kiosk;

            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (Int32)reader[0];
                    orderBasketID = result;
                }
            }
            Log.Info("Execute OrderBasketAdd - {0}", orderBasketID);

            return orderBasketID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="con"></param>
        /// <param name="basketId"></param>
        /// <param name="AKDURN"></param>
        /// <param name="quantity"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        private int ExecuteOrderBasketAddItem(SqlConnection con, int basketId, long AKDURN, int quantity, int parentId)
        {
            int itemId = -9;
            // Create and configure a command object       
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasketAddItem";
            com.Parameters.Add("@BasketID", SqlDbType.Int).Value = basketId;
            com.Parameters.Add("@AKDURN", SqlDbType.Int).Value = AKDURN;
            com.Parameters.Add("@quantity", SqlDbType.Int).Value = quantity;
            com.Parameters.Add("@parentItemID", SqlDbType.Int).Value = parentId;
            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (Int32)reader[0];
                    itemId = result;
                }
            }
            Log.Info("Execute OrderBasketAddItem - {0}", basketId);
            return itemId;
        }

        private string ExecuteOrderBasket_API_Checkbasket(SqlConnection con, int orderBasketID)
        {
            string jsonPayload = "";
            // Create and configure a command object  
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasket_API_CheckBasket";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;

            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (string)reader[0];
                    if (result == "-1" || result == "0")
                    {
                        Log.Error("\nExecute OrderBasket_API_Checkbasket - OrderBasketID = {0}: {1}\n", orderBasketID, result);
                    }
                    else {
                        jsonPayload = result;
                        Log.Info("\nExecute OrderBasket_API_Checkbasket - OrderBasketID = {0}: {1}\n", orderBasketID, jsonPayload);
                    }
                }
            }
            return jsonPayload;
        }


        private void ExecuteOrderBasket_APIResponse_Checkbasket(SqlConnection con, int orderBasketID, string jsonResponse)
        {                     
            // Create and configure a command object   
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasket_APIResponse_CheckBasket";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            com.Parameters.Add("@json", SqlDbType.VarChar).Value = jsonResponse;
            var result = com.ExecuteScalar();
            Log.Info("Execute OrderBasket_APIResponse_Checkbasket: OrderBasketID {0}: {1}", orderBasketID, result);
        }

    

        private string ExecuteOrderBasket_API_Order(SqlConnection con, int orderBasketID)
        {
            string jsonPayload = "";
            // Create and configure a command object   
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure; com.CommandText = "OrderBasket_API_Order";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (string)reader[0];
                    if (result == "-1" || result == "0")
                    {
                        Log.Error("\nExecute OrderBasket_API_Order - OrderBasketID = {0}: {1}\n", orderBasketID, result);
                    } else
                    {
                        jsonPayload = result;
                        Log.Info("\nExecute OrderBasket_API_Order - OrderBasketID = {0}: {1}\n", orderBasketID, jsonPayload);
                    }
                }
            }
            return jsonPayload;
        }

        private void ExecuteOrderBasket_APIResponse_Order(SqlConnection con, int orderBasketID, string jsonResponse)
        {
            // Create and configure a command object       
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasket_APIResponse_Order";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            com.Parameters.Add("@json", SqlDbType.VarChar).Value = jsonResponse;
            var result = com.ExecuteScalar();
            Log.Info("Execute OrderBasket_APIResponse_Order: OrderBasketID {0}: {1}", orderBasketID, result);
        }
        private string ExecuteOrderBasket_API_CollectionByCustomer(SqlConnection con, int orderBasketID)
        {
            string jsonPayload = "";

            // Create and configure a command object 
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasket_API_CollectionByCustomer";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            using (IDataReader reader = com.ExecuteReader())
            {
                while (reader.Read())
                {
                    var result = (string)reader[0];

                    if (result == "-1" || result == "0")
                    {
                        Log.Error("\nExecute OrderBasket_API_CollectionByCustomer - OrderBasketID = {0}: {1}\n", orderBasketID, result);
                    }
                    else
                    {
                        jsonPayload = result;
                        Log.Info("\nExecute OrderBasket_API_CollectionByCustomer - OrderBasketID = {0}: {1}\n", orderBasketID, jsonPayload);
                    }
                }
            }
            return jsonPayload;
        }

        private void ExecuteOrderBasket_APIResponse_CollectionByCustomer(SqlConnection con, int orderBasketID, string jsonResponse)
        {
            // Create and configure a command object    
            SqlCommand com = con.CreateCommand();
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = "OrderBasket_APIResponse_CollectionByCustomer";
            com.Parameters.Add("@OrderBasketID", SqlDbType.Int).Value = orderBasketID;
            com.Parameters.Add("@json", SqlDbType.VarChar).Value = jsonResponse;
            var result = com.ExecuteScalar();
            Console.WriteLine("Execute OrderBasket_APIResponse_CollectionByCustomer: OrderBasketID {0}: {1}", orderBasketID, result);
        }



    }
}
