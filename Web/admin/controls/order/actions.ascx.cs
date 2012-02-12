#region dashCommerce License
/*
The MIT License

Copyright (c) 2008 - 2010 Mettle Systems LLC, P.O. Box 181192 Cleveland Heights, Ohio 44118, United States

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion
using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using MettleSystems.dashCommerce.Core;
using MettleSystems.dashCommerce.Localization;
using MettleSystems.dashCommerce.Store;
using MettleSystems.dashCommerce.Store.Services.PaymentService;
using MettleSystems.dashCommerce.Store.Web.Controls;
using SubSonic.Utilities;

namespace MettleSystems.dashCommerce.Web.admin.controls.order {
  public partial class actions : AdminControl {
    
    #region Member Variables
    
    private int orderId = 0;
    private string view = string.Empty;
    DataSet ds = null;
    PaymentService paymentService = null;

    #endregion
    
    #region Page Events

    /// <summary>
    /// Handles the Load event of the Page control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
    protected void Page_Load(object sender, EventArgs e) {
      try {
        orderId = Utility.GetIntParameter("orderId");
        view = Utility.GetParameter("view");
        if(orderId > 0 && view == "a") {
          SetActionsProperties();
          if(!Page.IsPostBack) {
            Order order = new Order(orderId);
            ds = new OrderController().FetchRefundedOrderItems(orderId);
            Transaction transaction = new TransactionController().FetchByOrderIdAndTransactionTypeId(orderId, (int)TransactionType.Charge);

            if(transaction.GatewayName == "PayPal Standard") {
              lblActionMessage.Text = LocalizationUtility.GetText("lblPayPalStandardRefundInstructions");
            }
            
            if(order.OrderStatusDescriptorId == (int)OrderStatus.OrderFullyRefunded) {
              btnRefundTransaction.Visible = false;
              lblAdditionalRefundAmount.Visible = false;
              txtAdditionalRefundAmount.Visible = false;
              dgOrderItems.Visible = false;
            }
            else {
              dgOrderItems.DataSource = order.OrderItemCollection;
              dgOrderItems.ItemDataBound += new DataGridItemEventHandler(dgOrderItems_ItemDataBound);
              dgOrderItems.Columns[1].HeaderText = LocalizationUtility.GetText("hdrRefund");
              dgOrderItems.Columns[2].HeaderText = LocalizationUtility.GetText("hdrSku");
              dgOrderItems.Columns[3].HeaderText = LocalizationUtility.GetText("hdrQuantityRemaining");
              dgOrderItems.Columns[4].HeaderText = LocalizationUtility.GetText("hdrName");
              dgOrderItems.Columns[5].HeaderText = LocalizationUtility.GetText("hdrPricePaid");
              dgOrderItems.Columns[6].HeaderText = LocalizationUtility.GetText("hdrItemTax");
              dgOrderItems.Columns[7].HeaderText = LocalizationUtility.GetText("hdrDiscountAmount");
              dgOrderItems.DataBind();
            }
            
            OrderStatusDescriptorCollection orderStatusDescriptorCollection = new OrderStatusDescriptorController().FetchAll();
            ddlOrderStatus.DataSource = orderStatusDescriptorCollection;
            ddlOrderStatus.DataValueField = "OrderStatusDescriptorId";
            ddlOrderStatus.DataTextField = "Name";
            ddlOrderStatus.DataBind();
            
            ddlOrderStatus.SelectedValue = order.OrderStatusDescriptorId.ToString();
          }
        }
      }
      catch(Exception ex) {
        Logger.Error(typeof(actions).Name + ".Page_Load", ex);
        base.MasterPage.MessageCenter.DisplayCriticalMessage(ex.Message);
      }
    }

    void dgOrderItems_ItemDataBound(object sender, DataGridItemEventArgs e) {
      if(e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem) {
        OrderItem orderItem = e.Item.DataItem as OrderItem;
        Label lblNetAmount = e.Item.Cells[2].FindControl("lblNetAmount") as Label;
        int netQuantity = 0;
        if(ds.Tables[0].Rows.Count > 0) {
          for(int i = 0;i < ds.Tables[0].Rows.Count;i++) {
            if(ds.Tables[0].Rows[i]["Sku"].ToString() == orderItem.Sku) {
              netQuantity = orderItem.Quantity - (int)ds.Tables[0].Rows[i]["Quantity"];
            }
          }
        }
        else {
          netQuantity = orderItem.Quantity;
        }

        if(lblNetAmount != null) {
          lblNetAmount.Text = netQuantity.ToString();
        }
        RangeValidator rvRefundQuantity = e.Item.Cells[0].FindControl("rvRefundQuantity") as RangeValidator;
        if(rvRefundQuantity != null) {
          rvRefundQuantity.MinimumValue = "0";
          rvRefundQuantity.MaximumValue = netQuantity.ToString();
          rvRefundQuantity.ErrorMessage = string.Format(LocalizationUtility.GetText("lblRangeIsInvalid"), orderItem.Sku);
        }
      }
    }

    /// <summary>
    /// Handles the Click event of the btnRefundTransaction control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
    protected void btnRefundTransaction_Click(object sender, EventArgs e) {
      try {
        if (Page.IsValid) {
          TextBox txtRefundQuantity = null;
          Order order = new Order(orderId);
          OrderItem orderItem = null;
          OrderItem refundedOrderItem = null;
          Order refundOrder = new Order(order, WebUtility.GetUserName());
          foreach (DataGridItem item in dgOrderItems.Items) {
            txtRefundQuantity = item.Cells[0].FindControl("txtRefundQuantity") as TextBox;
            if (txtRefundQuantity != null) {
              int quantity = 0;
              bool isParsed = int.TryParse(txtRefundQuantity.Text, out quantity);
              if (isParsed) {
                orderItem = new OrderItem(item.Cells[0].Text);
                refundedOrderItem = new OrderItem();
                //refundedOrderItem.OrderId = orderItem.OrderId;
                refundedOrderItem.ProductId = orderItem.ProductId;
                refundedOrderItem.Name = orderItem.Name;
                refundedOrderItem.Sku = orderItem.Sku;
                refundedOrderItem.Quantity = quantity;
                refundedOrderItem.PricePaid = orderItem.PricePaid;
                refundedOrderItem.Attributes = orderItem.Attributes;
                refundedOrderItem.Weight = orderItem.Weight;
                refundedOrderItem.ItemTax = orderItem.ItemTax;
                refundedOrderItem.DiscountAmount = orderItem.DiscountAmount;
                refundOrder.OrderItemCollection.Add(refundedOrderItem);
              }
            }
          }
          decimal additionalRefundAmount = 0;
          decimal.TryParse(txtAdditionalRefundAmount.Text, out additionalRefundAmount);
          refundOrder.ShippingAmount = additionalRefundAmount;
          Transaction transaction = new TransactionController().FetchByOrderIdAndTransactionTypeId(orderId, (int)TransactionType.Charge);
          if (transaction.GatewayName == "PayPal Standard") {
            OrderController.RefundStandard(transaction, refundOrder, WebUtility.GetUserName());
          }
          else {
            OrderController.Refund(transaction, refundOrder, WebUtility.GetUserName());
          }
          base.MasterPage.MessageCenter.DisplaySuccessMessage(LocalizationUtility.GetText("lblOrderRefunded"));
        }
      }
      catch (PaymentServiceException pse) {
        Logger.Error(typeof(actions).Name + ".btnRefundTransaction_Click", pse);
        base.MasterPage.MessageCenter.DisplayFailureMessage(LocalizationUtility.GetPaymentProviderErrorText(pse.Message));
      }
      catch (Exception ex) {
        Logger.Error(typeof(actions).Name + ".btnRefundTransaction_Click", ex);
        base.MasterPage.MessageCenter.DisplayCriticalMessage(ex.Message);
      }
    }


    /// <summary>
    /// Handles the Click event of the btnOrderStatus control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
    protected void btnOrderStatus_Click(object sender, EventArgs e) {
      try {
        Order order = new Order(orderId);
        int orderStatusDescriptorId = order.OrderStatusDescriptorId;
        bool isParsed = int.TryParse(ddlOrderStatus.SelectedValue, out orderStatusDescriptorId);
        if(!isParsed) {
          orderStatusDescriptorId = order.OrderStatusDescriptorId;
        }
        order.OrderStatusDescriptorId = orderStatusDescriptorId;
        order.Save(WebUtility.GetUserName());
        base.MasterPage.MessageCenter.DisplaySuccessMessage(LocalizationUtility.GetText("lblOrderStatusChanged"));
       
      }
      catch(Exception ex) {
        Logger.Error(typeof(actions).Name + ".btnOrderStatus_Click", ex);
        base.MasterPage.MessageCenter.DisplayCriticalMessage(ex.Message);
      }
    }


    /// <summary>
    /// Handles the Click event of the btnManualTransaction control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    protected void btnManualTransaction_Click(object sender, EventArgs e) {
      try {
        Order order = new Order(orderId);
        paymentService = new PaymentService();
        if (order.PaymentMethod == "PayPal" && paymentService.PaymentServiceSettings.ProviderSettingsCollection.Find(provider => provider.GetType().Name.Contains("PayPalStandardPaymentProvider")) != null) {
          OrderController.CommitStandardTransaction(order, txtTransactionId.Text.Trim(), order.Total);
        }
        SetOrderStatus(order.OrderStatusDescriptorId.ToString());
        base.MasterPage.MessageCenter.DisplaySuccessMessage(LocalizationUtility.GetText("lblManualTransactionSucceeded"));
      }
      catch (Exception ex) {
        Logger.Error(typeof(actions).Name + ".btnManualTransaction_Click", ex);
        base.MasterPage.MessageCenter.DisplayCriticalMessage(LocalizationUtility.GetCriticalMessageText(ex.Message));
      }
    }

    #endregion
    
    #region Methods
    
    #region Protected
    
     /// <summary>
    /// Gets the formatted amount.
    /// </summary>
    /// <param name="total">The total.</param>
    /// <returns></returns>
    protected string GetFormattedAmount(string total) {
      return StoreUtility.GetFormattedAmount(total, true);
    }    
    
    #endregion
    
    #region Private

    private void SetActionsProperties() {
      this.Page.Title = LocalizationUtility.GetText("titleOrderActions");
    }

    /// <summary>
    /// Sets the order status.
    /// </summary>
    /// <param name="orderStatusId">The order status id.</param>
    private void SetOrderStatus(string orderStatusId) {
      OrderStatusDescriptorCollection orderStatusDescriptorCollection = new OrderStatusDescriptorController().FetchAll();
      ddlOrderStatus.DataSource = orderStatusDescriptorCollection;
      ddlOrderStatus.DataValueField = "OrderStatusId";
      ddlOrderStatus.DataTextField = "Name";
      ddlOrderStatus.DataBind();
      ddlOrderStatus.SelectedValue = orderStatusId;
    }

    
    #endregion
    
    #endregion
    
  }
}