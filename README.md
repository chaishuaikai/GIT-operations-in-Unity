# GIT-operations-in-Unity
Unity中操作Git
在untiy中向git推送拉取
# FileUploader.cs   
--这个脚本是向Git提交文件的，里边处理了，冲突的时候提交回滚
如果删除了一个文件，点击刷新，会提示你，是否把git远端仓库的文件删除
这里就不列举了。。。
# GitPuller.cs
--这个脚本是拉取Git内容的，如果是自己提交的文件，拉取的时候，如果本地有修改就不会覆盖，
如果是别人提交的文件，你修改了里边的内容，产生了冲突，需要手动解决。只要不是多个人操作一个文件，就没问题，如果需要两个人操作同一个文件的话，冲突问题就要手动解决了
# GitLog.cs
--这个脚本是展示分支中的日志，这个应该不用说那么多
# BranchManagement.cs
--这个脚本是拉取远端分支并创建本地，跟踪远程分支
# BranchManagement
--这个脚本是用来创建本地分支并推送到远端，还可以删除本地和远端分支

# 自白
--我只是一个unity的小白，喜欢就拿去用，不喜欢也别喷，有什么好一点的建议，咱们一起交流一起学习
