using CrmEarlyBound;
using Microsoft.Xrm.Sdk;
using System;
using System.Linq;

namespace Solvve.Test.Plugins
{
	public class PreValidateOrderPlugin : IPlugin
	{
		public void Execute(IServiceProvider serviceProvider)
		{
			ITracingService tracingService =
				(ITracingService)serviceProvider.GetService(typeof(ITracingService));

			IPluginExecutionContext context = (IPluginExecutionContext)
				serviceProvider.GetService(typeof(IPluginExecutionContext));

			if (context.InputParameters.Contains("Target") &&
				  context.InputParameters["Target"] is Entity)
			{
				Entity entity = (Entity)context.InputParameters["Target"];

				IOrganizationServiceFactory serviceFactory =
					(IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
				IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
				CrmServiceContext crmServiceContext = new CrmServiceContext(service);

				try
				{
					var contactEntity = crmServiceContext.ContactSet
					.Where(c => c.ContactId == ((EntityReference)entity["ib_orderer"]).Id)
					.FirstOrDefault();
					var companyId = contactEntity.ParentCustomerId.Id;

					var accountEntity = crmServiceContext.AccountSet.FirstOrDefault(x => x.Id == companyId);

					var currentTotalValue = (from g in crmServiceContext.ib_OrderSet
											 join c in crmServiceContext.ContactSet on g.ib_Orderer.Id equals c.Id
											 where c.ParentCustomerId.Id == companyId
											 select g.ib_Value.Value).ToList().Sum();

					var companysLimit = crmServiceContext.AccountSet
						.Where(x => x.Id == companyId)
						.FirstOrDefault()
						.ib_OverallGoodsValueLimit.Value;

					var limitLeft = companysLimit - currentTotalValue;

					if (((Money)entity["ib_value"]).Value > limitLeft)
					{
						tracingService.Trace($"Exeed limit for {accountEntity.Name}");
						throw new InvalidPluginExecutionException($"The value of this order goes beyond the {accountEntity.Name} company limit. current Limit Balance is {limitLeft}");
					}
				}
				catch (Exception ex)
				{
					tracingService.Trace("OrderLimitPlugin: {0}", ex.ToString());
					throw;
				}
			}

		}
	}
}
