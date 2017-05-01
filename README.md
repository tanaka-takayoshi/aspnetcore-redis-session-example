# Redis for HTTP session storage example

See my blog post for the detail.
- [Configure IDistributedCache and IDataProtection for session in ASP.NET Core](http://tech.en.tanaka733.net/entry/configure-idistributedcache-and-idataprotection-for-session-in-aspnet-core)
- [ASP.NET Core で複数Webサーバーでセッションを共有するときは、IDistributedCacheとIDataProtectionに注意しないといけない話](http://tech.tanaka733.net/entry/session-sharing-in-aspnetcore)

# How to set up on OpenShift

```
$ oc import-image my-rhscl/redis-32-rhel7 --from=registry.access.redhat.com/rhscl/redis-32-rhel7 -n openshift --confirm
$ export REDIS_NAME=redis-session
$ oc new-app redis-32-rhel7:latest --name=$REDIS_NAME
$ oc new-app dotnetcore-11-rhel7~https://github.com/tanaka-takayoshi/aspnetcore-redis-session-example.git --env=REDIS_SESSION_SERVICE_NAME=$REDIS_NAME --name=aspnetcore-sessionexample
```
