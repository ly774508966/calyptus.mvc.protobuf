using System;
using System.Web;
using Calyptus.Mvc;
using Google.ProtocolBuffers;
using Google.ProtocolBuffers.Descriptors;

namespace Calyptus.Mvc.Protobuf
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class ProtobufControllerAttribute : ControllerBaseAttribute
	{
		private const string CONTENT_TYPE = "application/vnd.google.protobuf";

		public override void Initialize(Type controllerType) { }

		public override void SerializeToPath(IRouteAction action, IPathStack path) { }

		public override bool TryBinding(IHttpContext context, IPathStack path, out IHttpHandler handler)
		{
			// Entry controller not yet supported
			handler = null;
			return false;
		}

		public override bool TryBinding(IHttpContext context, IPathStack path, object controller, out IHttpHandler handler)
		{
			if (path.IsAtEnd){ handler = null; return false; }
			string method = path.Pop();
			if (!path.IsAtEnd){ handler = null; return false; }

			IService service = controller as IService;
			if (service == null) throw new Exception("ProtobufControllerAttribute can only be applied to classes implementing IService.");

			var descriptor = service.DescriptorForType.FindMethodByName(method);
			if (descriptor == null){ handler = null; return false; }

			IMessage request;

			string verb = context.Request.HttpMethod;
			if (verb == "GET")
			{
				request = null;
			}
			else if (verb == "POST" && context.Request.ContentType == CONTENT_TYPE)
			{
				request =
					service.GetRequestPrototype(descriptor)
					.WeakToBuilder()
					.WeakMergeFrom(CodedInputStream.CreateInstance(context.Request.InputStream))
					.WeakBuild();
			}
			else
			{
				handler = null;
				return false;
			}

			handler = new ProtobufServiceHandler(service, descriptor, request);
			return true;
		}

		private class ProtobufServiceHandler : IHttpHandler
		{
			private IService service;
			private MethodDescriptor method;
			private IMessage request;

			public ProtobufServiceHandler(IService service, MethodDescriptor method, IMessage request)
			{
				this.service = service;
				this.method = method;
				this.request = request;
			}

			public bool IsReusable
			{
				get { return false; }
			}

			public void ProcessRequest(HttpContext context)
			{
				var r = context.Response;
				bool called = false;
				Action<IMessage> mess = (response) =>
				{
					called = true;
					r.ContentType = CONTENT_TYPE;
					r.StatusCode = 200;
					if (response != null) response.WriteTo(r.OutputStream);
				};

				try
				{
					service.CallMethod(method, null, request, mess);
					if (!called) throw new Exception("The service have to call done before returning. Async not supported yet.");
				}
				catch (Exception e)
				{
					r.StatusCode = 500;
					r.ContentType = "text/plain";
					r.Write(e.Message);
					r.Write("\n");
					r.Write(e.StackTrace);
				}
			}
		}

	}
}
