<script setup lang="ts">
import { AlertCircle, Database, Loader2 } from 'lucide-vue-next'
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '@/api/client'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'

const route = useRoute()
const router = useRouter()
const { state, login } = useAuth()

const username = ref('')
const password = ref('')
const error = ref<ApiError | null>(null)

async function submitLogin() {
  error.value = null

  try {
    await login(username.value.trim(), password.value)
    const target = state.currentTenant
      ? String(route.query.redirect || '/')
      : '/tenant/select'
    await router.replace(target)
  }
  catch (loginError) {
    error.value = loginError instanceof ApiError
      ? loginError
      : new ApiError(0, '登录失败，请稍后重试。')
  }
}
</script>

<template>
  <main class="min-h-screen bg-background text-foreground">
    <div class="grid min-h-screen lg:grid-cols-[0.95fr_1.05fr]">
      <section class="hidden border-r bg-card lg:flex lg:flex-col lg:justify-between">
        <div class="p-10">
          <div class="inline-flex items-center gap-3 rounded-md border bg-background px-3 py-2 text-sm font-medium">
            <Database :size="18" class="text-primary" />
            Sekai Platform
          </div>
        </div>

        <div class="space-y-6 p-10">
          <p class="max-w-xl text-4xl font-semibold leading-tight tracking-normal">
            字幕组语言资产工作台
          </p>
          <p class="max-w-lg text-sm leading-6 text-muted-foreground">
            登录后可访问当前租户的剧情资产、译文版本、导入入口和管理能力。
          </p>
          <p class="rounded-md border bg-background p-4 text-xs text-muted-foreground">
            当前连接由开发服务器代理或部署环境统一转发，登录 Cookie 按同源方式保存。
          </p>
        </div>
      </section>

      <section class="flex items-center justify-center px-4 py-10">
        <Card class="w-full max-w-md">
          <CardHeader>
            <CardTitle class="text-xl">
              登录
            </CardTitle>
            <p class="text-sm text-muted-foreground">
              使用 QQ 号和密码进入平台。
            </p>
          </CardHeader>
          <CardContent>
            <form class="space-y-4" @submit.prevent="submitLogin">
              <label class="block space-y-2">
                <span class="text-sm font-medium">QQ 号</span>
                <input
                  v-model="username"
                  class="w-full rounded-md border bg-background px-3 py-2 text-sm outline-none transition focus:ring-2 focus:ring-ring"
                  autocomplete="username"
                  required
                >
              </label>

              <label class="block space-y-2">
                <span class="text-sm font-medium">密码</span>
                <input
                  v-model="password"
                  class="w-full rounded-md border bg-background px-3 py-2 text-sm outline-none transition focus:ring-2 focus:ring-ring"
                  type="password"
                  autocomplete="current-password"
                  required
                >
              </label>

              <div v-if="error" class="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm">
                <div class="flex gap-2 text-destructive">
                  <AlertCircle :size="18" />
                  <span>{{ error.message }}</span>
                </div>
                <p v-if="error.traceId" class="mt-2 text-xs text-muted-foreground">
                  trace_id: {{ error.traceId }}
                </p>
              </div>

              <Button class="w-full" :disabled="state.loading">
                <Loader2 v-if="state.loading" :size="16" class="mr-2 animate-spin" />
                登录
              </Button>
            </form>
          </CardContent>
        </Card>
      </section>
    </div>
  </main>
</template>
