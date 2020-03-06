using GenericPOSRestService.Common;
using GenericPOSRestService.Common.ServiceCallClasses;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
                /*******************************************************************************
                * 1) Call the OrderBasketAdd stored proc to get the Id for the new Basket- 
                * this must be called first for a new transaction
                * ******************************************************************************/
                basketId = ExecuteOrderBasketAdd(con);

                if (basketId < 1)
                {
                    Log.Error("Order Basket Add Failed ");
                    return;
                }
                Log.Info($"BasketId = {basketId}");
                /*******************************************************************************
                * 2) Call the OrderBasketAddItem stored proc to get the parentId
                * ******************************************************************************/ 

                 
                for (int i = 0; i < request.DOTOrder.Items.Count; i++)
                {
                    Item item = request.DOTOrder.Items[i];

                    ProcessItem(con, item, basketId, 0);

                }

               // 3) do check basket
                    payLoad = ExecuteOrderBasket_API_Checkbasket(con, basketId);

                // get the API response for checkBasket check if the API calls succeeds
                IRestResponse checkBasketResp = restCalls.PostCheckBasket(payLoad);

                //deserialise the contents of checkbasket
                dynamic checkBasket = JsonConvert.DeserializeObject<dynamic>(checkBasketResp.Content);


                //4) Update orderBasket with API Response       
                ExecuteOrderBasket_APIResponse_Checkbasket(con, basketId, payLoad);
                Console.WriteLine(Environment.NewLine);

                //5  Collection By Customer)   
                payLoad = "";
                payLoad = ExecuteOrderBasket_API_CollectionByCustomer(con, basketId);
                Console.WriteLine(Environment.NewLine);

                //(6  Update orderBasket with API Response         
                //ExecuteOrderBasket_APIResponse_CollectionByCustomer(con, basketId, jsonResponse);
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

        public string ExecuteOrderBasket_API_Checkbasket(SqlConnection con, int orderBasketID)
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
                        Log.Info("Execute OrderBasket_API_Checkbasket - OrderBasketID = {0}: {1}", orderBasketID, result);
                    }
                    else {
                        jsonPayload = result;
                        Log.Info("Execute OrderBasket_API_Checkbasket - OrderBasketID = {0}: {1}", orderBasketID, jsonPayload);
                    }
                }
            }
            return jsonPayload;
        }


        public void ExecuteOrderBasket_APIResponse_Checkbasket(SqlConnection con, int orderBasketID, string jsonResponse)
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

        public string ExecuteOrderBasket_API_CollectionByCustomer(SqlConnection con, int orderBasketID)
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
                        Log.Info("Execute OrderBasket_API_CollectionByCustomer - OrderBasketID = {0}: {1}", orderBasketID, result);
                    }
                    else
                    {
                        jsonPayload = result;
                        Log.Info("Execute OrderBasket_API_CollectionByCustomer - OrderBasketID = {0}: {1}", orderBasketID, jsonPayload);
                    }
                }
            }
            return jsonPayload;
        }
  }
}
