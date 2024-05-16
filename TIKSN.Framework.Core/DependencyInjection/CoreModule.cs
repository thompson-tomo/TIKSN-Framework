using Autofac;
using TIKSN.Data.Mongo;
using TIKSN.Data.RavenDB;
using TIKSN.Licensing;
using TIKSN.Mapping;
using TIKSN.Serialization;
using TIKSN.Web.Rest;

namespace TIKSN.DependencyInjection;

public class CoreModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        _ = builder.RegisterType<DotNetXmlDeserializer>().AsSelf().SingleInstance();
        _ = builder.RegisterType<DotNetXmlSerializer>().AsSelf().SingleInstance();
        _ = builder.RegisterType<JsonDeserializer>().AsSelf().SingleInstance();
        _ = builder.RegisterType<JsonSerializer>().AsSelf().SingleInstance();
        _ = builder.RegisterType<RestRequester>().As<IRestRequester>();
        _ = builder.RegisterType<SerializationRestFactory>().As<ISerializerRestFactory>().As<IDeserializerRestFactory>()
            .SingleInstance();

        _ = builder.RegisterGeneric(typeof(IdentityMapper<>))
            .As(typeof(IMapper<,>))
            .As(typeof(IAsyncMapper<,>))
            .SingleInstance();

        _ = builder.RegisterType<MongoClientSessionContext>()
            .As<IMongoClientSessionStore>()
            .As<IMongoClientSessionProvider>()
            .InstancePerLifetimeScope();

        _ = builder
            .RegisterType<RavenSessionContext>()
            .As<IRavenSessionStore>()
            .As<IRavenSessionProvider>()
            .InstancePerLifetimeScope();

        _ = builder.RegisterGeneric(typeof(LicenseFactory<,>))
            .As(typeof(ILicenseFactory<,>))
            .SingleInstance();

        _ = builder
            .RegisterType<Finance.ForeignExchange.Data.EntityFrameworkCore.DataEntityMapper>()
            .AsImplementedInterfaces()
            .SingleInstance();

        _ = builder
            .RegisterType<Finance.ForeignExchange.Data.LiteDB.DataEntityMapper>()
            .AsImplementedInterfaces()
            .SingleInstance();

        _ = builder
            .RegisterType<Finance.ForeignExchange.Data.Mongo.DataEntityMapper>()
            .AsImplementedInterfaces()
            .SingleInstance();

        _ = builder
            .RegisterType<Finance.ForeignExchange.Data.RavenDB.DataEntityMapper>()
            .AsImplementedInterfaces()
            .SingleInstance();
    }
}
