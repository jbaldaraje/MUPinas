#include "doctest.h"
#include "GameLogic/Testing/TestingSession.h"

TEST_CASE("Testing status accepts authority only from valid server records")
{
    using namespace GameLogic::Testing;
    ResetSession();
    CHECK_FALSE(ReceiveStatus(L"ordinary server notice"));
    CHECK_FALSE(Session().administrator);
    CHECK(ReceiveStatus(L"@LT1 status 1 10000 1000 800 65535 1"));
    CHECK(Session().administrator);
    CHECK(Session().savedSpeed == 800);
    CHECK(ReceiveStatus(L"@LT1 status 1 10000 1000 999999 65535 0"));
    CHECK(Session().savedSpeed == 800);
    CHECK(Session().administrator);
    ResetSession();
    CHECK_FALSE(Session().administrator);
}

TEST_CASE("Testing response rows stay bounded and refresh clears old entities")
{
    using namespace GameLogic::Testing;
    ResetSession();
    for (int i = 0; i < 200; ++i)
        ReceiveStatus(L"@LT1 bot TestCrowd 0");
    CHECK(Session().bots.size() == 100);
    ReceiveStatus(L"@LT1 status 0 10000 1000 800 65535 0");
    CHECK(Session().bots.empty());
    ReceiveStatus(L"@LT1 error denied");
    CHECK(Session().error == "denied");
}
