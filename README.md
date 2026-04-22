.git/hooks/commit-msg

#!/bin/sh
# Lấy nội dung message từ file tạm mà Git tạo ra
commit_msg=$(cat "$1")

# Regex: Bắt đầu bằng [AI][JIRA-XXX] hoặc [JIRA-XXX]
# Cho phép có nội dung phía sau
regex="^(\[AI\])?\[[A-Z]+-[0-9]+\].*"

if ! echo "$commit_msg" | grep -Eq "$regex"; then
    echo "--------------------------------------------------"
    echo "ERROR: Commit message sai định dạng quy định!"
    echo "Mẫu chuẩn: [AI][JIRA-123] Nội dung commit"
    echo "Hoặc: [JIRA-123] Nội dung commit"
    echo "--------------------------------------------------"
    exit 1
fi