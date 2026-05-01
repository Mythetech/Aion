using Aion.Contracts.Queries;
using Aion.Core.Database.PostgreSQL;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class PostgreSqlPlanParserTests
{
    private readonly PostgreSqlPlanParser _sut = new();

    [Fact]
    public void Parse_SimpleSeqScan_ReturnsTreeWithSingleNode()
    {
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = "Seq Scan on users  (cost=0.00..35.50 rows=2550 width=36)\n"
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.Operation.ShouldBe("Seq Scan");
        tree.Root.Target.ShouldBe("users");
        tree.Root.StartupCost.ShouldBe(0.00);
        tree.Root.TotalCost.ShouldBe(35.50);
        tree.Root.EstimatedRows.ShouldBe(2550);
        tree.Root.Children.ShouldBeEmpty();
        tree.TotalCost.ShouldBe(35.50);
    }

    [Fact]
    public void Parse_HashJoinWithChildren_ReturnsCorrectTree()
    {
        var planText = """
            Hash Join  (cost=1.09..2.19 rows=1 width=72)
              Hash Cond: (o.customer_id = c.id)
              ->  Seq Scan on orders o  (cost=0.00..1.05 rows=5 width=44)
                    Filter: (total > 100)
              ->  Hash  (cost=1.04..1.04 rows=4 width=36)
                    ->  Seq Scan on customers c  (cost=0.00..1.04 rows=4 width=36)
            """;

        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = planText
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.Operation.ShouldBe("Hash Join");
        tree.Root.JoinCondition.ShouldBe("(o.customer_id = c.id)");
        tree.Root.Children.Count.ShouldBe(2);

        var seqScan = tree.Root.Children[0];
        seqScan.Operation.ShouldBe("Seq Scan");
        seqScan.Target.ShouldBe("orders o");
        seqScan.Filter.ShouldBe("(total > 100)");

        var hash = tree.Root.Children[1];
        hash.Operation.ShouldBe("Hash");
        hash.Children.Count.ShouldBe(1);
        hash.Children[0].Operation.ShouldBe("Seq Scan");
        hash.Children[0].Target.ShouldBe("customers c");
    }

    [Fact]
    public void Parse_IndexScan_ExtractsIndexTarget()
    {
        var planText = """
            Index Scan using users_pkey on users  (cost=0.15..8.17 rows=1 width=36)
              Index Cond: (id = 1)
            """;

        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = planText
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.Operation.ShouldBe("Index Scan using users_pkey");
        tree.Root.Target.ShouldBe("users");
        tree.Root.Properties.ShouldContainKey("Index Cond");
    }

    [Fact]
    public void Parse_SortAndAggregate_HandlesMultipleLevels()
    {
        var planText = """
            Aggregate  (cost=37.75..37.76 rows=1 width=8)
              ->  Sort  (cost=37.50..37.56 rows=25 width=4)
                    Sort Key: age
                    ->  Seq Scan on users  (cost=0.00..36.75 rows=25 width=4)
                          Filter: (active = true)
            """;

        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = planText
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.Operation.ShouldBe("Aggregate");
        tree.Root.Children.Count.ShouldBe(1);

        var sort = tree.Root.Children[0];
        sort.Operation.ShouldBe("Sort");
        sort.SortKey.ShouldBe("age");
        sort.Children.Count.ShouldBe(1);

        var scan = sort.Children[0];
        scan.Operation.ShouldBe("Seq Scan");
        scan.Target.ShouldBe("users");
        scan.Filter.ShouldBe("(active = true)");
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsNull()
    {
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = ""
        };

        var tree = _sut.Parse(plan);

        tree.ShouldBeNull();
    }

    [Fact]
    public void Parse_ErrorContent_ReturnsNull()
    {
        var plan = new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = "Error getting plan: connection refused"
        };

        var tree = _sut.Parse(plan);

        tree.ShouldBeNull();
    }

    [Fact]
    public void Parse_AnalyzePlan_ExtractsActualStats()
    {
        var planText = """
            Hash Join  (cost=1.09..2.19 rows=1 width=72) (actual time=0.045..0.052 rows=2 loops=1)
              Hash Cond: (o.customer_id = c.id)
              ->  Seq Scan on orders o  (cost=0.00..1.05 rows=5 width=44) (actual time=0.008..0.012 rows=5 loops=1)
                    Filter: (total > 100)
                    Rows Removed by Filter: 3
              ->  Hash  (cost=1.04..1.04 rows=4 width=36) (actual time=0.020..0.020 rows=4 loops=1)
                    Buckets: 1024  Batches: 1  Memory Usage: 9kB
                    ->  Seq Scan on customers c  (cost=0.00..1.04 rows=4 width=36) (actual time=0.004..0.006 rows=4 loops=1)
            """;

        var plan = new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT",
            PlanContent = planText
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.ActualRows.ShouldBe(2);
        tree.Root.ActualTime.ShouldBe(0.052);
        tree.Root.Loops.ShouldBe(1);

        var seqScan = tree.Root.Children[0];
        seqScan.ActualRows.ShouldBe(5);
        seqScan.Properties.ShouldContainKey("Rows Removed by Filter");
    }

    [Fact]
    public void Parse_AnalyzePlan_LargeRowDiscrepancy_PreservesValues()
    {
        var planText = """
            Seq Scan on events  (cost=0.00..1500.00 rows=10 width=64) (actual time=0.100..45.500 rows=50000 loops=1)
              Filter: (type = 'click')
              Rows Removed by Filter: 150000
            """;

        var plan = new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT",
            PlanContent = planText
        };

        var tree = _sut.Parse(plan);

        tree.ShouldNotBeNull();
        tree.Root.EstimatedRows.ShouldBe(10);
        tree.Root.ActualRows.ShouldBe(50000);
    }
}
