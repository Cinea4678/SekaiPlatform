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
            汇集剧情原文、历史译文和行级搜索，让翻译前的查证、对照和版本整理更顺手。
          </p>
        </div>
      </section>

      <section class="flex items-center justify-center px-4 py-10">
        <div class="w-full max-w-md">
          <Card>
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

          <Card class="mt-3 border-primary/20 bg-primary/5">
            <CardContent class="space-y-3 p-4">
              <div class="flex items-center justify-between gap-3">
                <div>
                  <p class="text-sm font-medium">
                    QQ 快捷登录
                  </p>
                  <p class="mt-1 text-xs text-muted-foreground">
                    授权能力正在接入。
                  </p>
                </div>
                <span class="rounded-md border border-primary/20 bg-background px-2 py-1 text-xs text-primary">
                  申请中
                </span>
              </div>
              <Button
                class="w-full bg-blue-600 text-white opacity-60 hover:bg-blue-600"
                disabled
              >
                <svg
                  class="mr-2 h-4 w-4"
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 640 640"
                  aria-hidden="true"
                >
                  <path
                    fill="currentColor"
                    d="M530.1 484.4c-11.5 1.4-44.9-52.7-44.9-52.7c0 31.3-16.1 72.2-51 101.8c16.8 5.2 54.8 19.2 45.8 34.4c-7.3 12.3-125.5 7.9-159.6 4c-34.1 3.8-152.3 8.3-159.6-4c-9-15.2 28.9-29.2 45.8-34.4c-34.9-29.5-51.1-70.4-51.1-101.8c0 0-33.3 54.1-44.9 52.7c-5.4-.6-12.4-29.6 9.3-99.7c10.3-33 22-60.5 40.1-105.8c-3.1-116.9 45.2-215 160.3-215c113.7 0 163.2 96.1 160.3 215c18.1 45.2 29.9 72.9 40.1 105.8c21.8 70.1 14.7 99.1 9.3 99.7z"
                  />
                </svg>
                使用QQ登录
              </Button>
            </CardContent>
          </Card>
        </div>
      </section>
    </div>
  </main>
</template>
