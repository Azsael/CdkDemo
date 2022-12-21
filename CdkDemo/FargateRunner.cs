using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Route53;
using System.Collections.Generic;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudWatch;

namespace CdkDemo;

internal static class FargateRunner
{
    public static void BuildFargateRaw(this Stack stack, Network network)
    {
        var cluster = new Cluster(stack, "raw-cluster", new ClusterProps
        {
            Vpc = network.Vpc,
            ClusterName = network.GenerateResourceName("raw-cluster"),
            ContainerInsights = true,
        });

        var hostedZone = new HostedZone(stack, "raw-host", new HostedZoneProps { ZoneName = "bookwana.com" });

        var certificate = new DnsValidatedCertificate(stack, "cert", new DnsValidatedCertificateProps
        {
            HostedZone = hostedZone,
            DomainName = "test.bookwana.com",
        });

        var dbSecurityGroup = SecurityGroup.FromLookupByName(stack, "raw-db-security", "", network.Vpc);

        var securityGroup = new SecurityGroup(stack, "raw-security-boop", new SecurityGroupProps());

        dbSecurityGroup.AddIngressRule(securityGroup, Port.Tcp(3306));
        securityGroup.AddEgressRule(dbSecurityGroup, Port.AllTcp());


        var service = new FargateService(stack, "", new FargateServiceProps
        {
            Cluster = cluster,
            CircuitBreaker = new DeploymentCircuitBreaker { Rollback = true },
            VpcSubnets = network.PrivateSubnets,
            DesiredCount = 1,
            ServiceName = network.GenerateResourceName("raw-service"),
            SecurityGroups = new[] { securityGroup },
            HealthCheckGracePeriod = Duration.Seconds(5),
            TaskDefinition = new FargateTaskDefinition(stack, "raw-task", new FargateTaskDefinitionProps
            {
                Cpu = 1024,
                MemoryLimitMiB = 2048,

            })
            {
                DefaultContainer = new ContainerDefinition(stack, "container", new ContainerDefinitionProps
                {
                    Image = ContainerImage.FromEcrRepository(Repository.FromRepositoryName(stack, "", "repo-anme"), "VERSION"),
                    Essential = true,
                    Environment = new Dictionary<string, string>
                    {
                        { "ASPNETCORE_ENVIRONMENT", "Production" }, // add enviro variables here
                    },

                    Secrets = null, // add secrets here
                })
            }
        });
        
        var loadBalancer = new ApplicationLoadBalancer(stack, "elb", new ApplicationLoadBalancerProps
        {
            Vpc = network.Vpc,
            Http2Enabled = true,
            VpcSubnets = network.PublicSubnets,
            InternetFacing = true,
            IpAddressType = IpAddressType.DUAL_STACK,
            LoadBalancerName = "",
            
        });

        loadBalancer.AddRedirect(new ApplicationLoadBalancerRedirectConfig
        {
            TargetProtocol = ApplicationProtocol.HTTPS,
            SourceProtocol = ApplicationProtocol.HTTP
        });

        var listener = loadBalancer.AddListener("listerner", new ApplicationListenerProps
        {
            Protocol = ApplicationProtocol.HTTPS,
            Certificates = new IListenerCertificate[] { ListenerCertificate.FromCertificateManager(certificate) }

        });

        listener.AddTargets("trargets", new AddApplicationTargetsProps
        {
            Targets = new[] { service }
            // health rules
        });

        // service.TaskDefinition.TaskRole
        // use this role to grant access to s3, param store, aws features, etc

        // alarm example
        service.MetricCpuUtilization().CreateAlarm(stack, "", new CreateAlarmOptions
        {
            EvaluationPeriods = 3,
            TreatMissingData = TreatMissingData.IGNORE,
            Threshold = 50,
        });

        var scaling = service.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = 5, // this overrides above desired
            MaxCapacity = 20,

        });

        // many options for scaling
        scaling.ScaleOnCpuUtilization("", new CpuUtilizationScalingProps
        {
            TargetUtilizationPercent = 20,
            // many more options
        });
    }
}