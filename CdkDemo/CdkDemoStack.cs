using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace CdkDemo;

public class CdkDemoStack : Stack
{
    internal CdkDemoStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        // ideally have app-settings and some config setup 

        // VPC / Subnet import code should be shared code for multiple stack use :)
        var vpc = Vpc.FromLookup(scope, "vpc", new VpcLookupOptions { VpcName = "" }); // Lookup via appropriate tags

        var privateSubnets = new SubnetSelection
        {
            SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
            Subnets = new[]
            {
                Subnet.FromSubnetId(scope, "subnet-1", "TODO"), // etc
            }
        };
        var network = new Network
        {
            Vpc = vpc,
            PrivateSubnets = privateSubnets
        };
        
        this.BuildFargateViaPattern(network);
        this.BuildFargateRaw(network);
    }
}